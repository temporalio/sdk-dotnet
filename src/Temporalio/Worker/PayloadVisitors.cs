#pragma warning disable SA1600 // Internal implementation plumbing.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    internal static partial class PayloadVisitors
    {
        // Transforms one payload into its replacement. A single (non-null) field needs exactly one
        // payload back, so it stays separate from the batch transform below. The token is cancelled
        // when the caller cancels or a concurrent sibling transform fails, so a transform doing I/O
        // forwards it and stops once a sibling has failed the walk.
        internal delegate Task<Payload> PayloadVisitor(
            Payload payload, CancellationToken cancellationToken);

        // Transforms a batch of payloads into their replacements, in any number. Fields that may hold
        // several payloads use this so a codec that packs or splits sees the whole field at once. The
        // token is cancelled on caller cancellation or a sibling transform's failure.
        internal delegate Task<IReadOnlyCollection<Payload>> PayloadsVisitor(
            IReadOnlyCollection<Payload> payloads, CancellationToken cancellationToken);

        /// <summary>
        /// Applies the transforms in <paramref name="options"/> to every payload reachable from the
        /// message <paramref name="root"/>, writing each replacement back in place. Pass the
        /// generated <c>Walk</c> overload for the root's message type.
        /// </summary>
        /// <typeparam name="T">The root message type.</typeparam>
        /// <param name="root">The message to walk.</param>
        /// <param name="walk">The generated walker for <typeparamref name="T"/>.</param>
        /// <param name="options">The transforms, concurrency limit, and cancellation.</param>
        /// <returns>A task that completes when every payload has been transformed.</returns>
        internal static Task VisitAsync<T>(
            T root, Action<T, WalkEnv, VisitState> walk, VisitOptions options) =>
            RunVisitAsync(options, (env, state) => walk(root, env, state));

        /// <summary>Visit a lone payload, transforming it in place.</summary>
        /// <param name="root">The payload to transform.</param>
        /// <param name="options">The transforms, concurrency limit, and cancellation.</param>
        /// <returns>A task that completes when the payload has been transformed.</returns>
        internal static Task VisitAsync(Payload root, VisitOptions options) =>
            RunVisitAsync(options, (env, state) => VisitSingle(
                state,
                env,
                root,
                replacement =>
                {
                    if (!ReferenceEquals(replacement, root))
                    {
                        root.ReplaceWith(replacement);
                    }
                }));

        /// <summary>Visit a lone batch of payloads, transforming them in place.</summary>
        /// <param name="root">The payloads to transform.</param>
        /// <param name="options">The transforms, concurrency limit, and cancellation.</param>
        /// <returns>A task that completes when the payloads have been transformed.</returns>
        internal static Task VisitAsync(RepeatedField<Payload> root, VisitOptions options) =>
            RunVisitAsync(options, (env, state) => VisitBatch(state, env, root));

        // Visits a single payload field, deferring the write-back of its replacement through the
        // setter. A null field is left alone.
        internal static void VisitSingle(
            VisitState state, WalkEnv env, Payload? payload, Action<Payload> set)
        {
            if (payload == null)
            {
                return;
            }

            Payload? captured = null;
            state.Pending.Add(
                () => CaptureAsync(() => env.VisitPayloadAsync(payload), result => captured = result));
            state.Writes.Add(() => set(captured!));
        }

        // Visits a payload-bearing field as one batch, deferring the in-place rewrite of the field.
        internal static void VisitBatch(
            VisitState state, WalkEnv env, RepeatedField<Payload>? payloads)
        {
            if (payloads == null || payloads.Count == 0)
            {
                return;
            }

            var snapshot = payloads.ToArray();
            IReadOnlyCollection<Payload>? captured = null;
            state.Pending.Add(
                () => CaptureAsync(() => env.VisitPayloadsAsync(snapshot), result => captured = result));
            state.Writes.Add(() =>
            {
                payloads.Clear();
                payloads.Add(captured!);
            });
        }

        // Visits each payload of a field on its own, since they are independent values rather than
        // one batch, and defers the in-place rewrite of each.
        internal static void VisitEach(VisitState state, WalkEnv env, RepeatedField<Payload> payloads)
        {
            for (var i = 0; i < payloads.Count; i++)
            {
                var index = i;
                VisitSingle(state, env, payloads[i], replacement => payloads[index] = replacement);
            }
        }

        internal static bool TryGetEnvelopeWalk(
            Payload payload,
            out string messageType,
            out Func<Payload, WalkEnv, Task<Payload>>? walk)
        {
            if (!payload.Metadata.TryGetValue("messageType", out var messageByteString))
            {
                walk = null;
                messageType = "<missing>";
                return false;
            }

            messageType = messageByteString.ToStringUtf8();
            if (!EnvelopeVisitors.TryGetValue(messageType, out walk))
            {
                walk = null;
                return false;
            }

            return true;
        }

        // Runs the work and captures its result so a later write-back can read it. The drain invokes
        // this, so when the work starts is the drain's to decide.
        private static async Task CaptureAsync<TValue>(
            Func<Task<TValue>> start, Action<TValue> capture) =>
            capture(await start().ConfigureAwait(false));

        private static async Task RunVisitAsync(
            VisitOptions options, Action<WalkEnv, VisitState> walk)
        {
            using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                options.CancellationToken);
            // A serial walk needs no gate: the drain runs one transform at a time, so there is nothing
            // to bound. A concurrent walk allocates one semaphore shared across the whole walk,
            // envelopes included, so the cap is global rather than per nesting level. Disposed after
            // the drain, by when every task has settled.
            var permits = Math.Max(1, options.MaxConcurrency);
            using var gate = permits > 1 ? new SemaphoreSlim(permits, permits) : null;
            var env = new WalkEnv(
                options.TransformPayload, options.TransformPayloads, gate, cancellationSource);
            var state = new VisitState();
            walk(env, state);
            await ApplyAsync(env, state).ConfigureAwait(false);
        }

        // Runs every deferred transform, then applies the write-backs. The write-backs mutate shared
        // message objects, so they run one at a time here rather than from the transform
        // continuations, which may have run concurrently.
        private static async Task ApplyAsync(WalkEnv env, VisitState state)
        {
            await DrainAsync(env, state.Pending).ConfigureAwait(false);
            foreach (var write in state.Writes)
            {
                write();
            }
        }

        // Runs the deferred transforms. A serial walk awaits them one at a time, touching no gate; a
        // concurrent walk starts them all and lets the shared gate bound how many run at once, then
        // surfaces the first real failure in traversal order rather than a sibling's cancellation.
        private static async Task DrainAsync(WalkEnv env, List<Func<Task>> pending)
        {
            if (env.Sequential)
            {
                foreach (var operation in pending)
                {
                    await operation().ConfigureAwait(false);
                }

                return;
            }

            var tasks = pending.Select(operation => operation()).ToList();
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception whenAllFailure)
            {
                foreach (var task in tasks)
                {
                    if (task.IsFaulted)
                    {
                        ExceptionDispatchInfo.Capture(task.Exception!.InnerExceptions[0]).Throw();
                    }
                }

                // Nothing faulted, so the failure was a cancellation; surface it as-is.
                ExceptionDispatchInfo.Capture(whenAllFailure).Throw();
                throw;
            }
        }

        // Re-serializes a System Nexus envelope around its walked message. The inner payloads are
        // transformed under the shared env, so they run against the same limit without this frame
        // holding one.
        private static async Task<Payload> VisitEnvelopeAsync<T>(
            Payload payload, Action<T, WalkEnv, VisitState> walk, WalkEnv env)
            where T : IMessage<T>, new()
        {
            BinaryProtoConverter.AssertProtoPayload(payload, typeof(T));
            var message = new T();
            message.MergeFrom(payload.Data);
            var state = new VisitState();
            walk(message, env, state);
            await ApplyAsync(env, state).ConfigureAwait(false);
            payload.Metadata.Clear();
            payload.Metadata["encoding"] = ByteString.CopyFromUtf8("binary/protobuf");
            payload.Metadata["messageType"] = ByteString.CopyFromUtf8(message.Descriptor.FullName);
            SystemNexusPayloads.MarkSystemPayload(payload);
            payload.Data = message.ToByteString();
            return payload;
        }

        // The deferred transforms of one walk and the write-backs to apply once they finish. The
        // transforms are thunks, not started tasks, so the drain decides when and how many run.
        internal sealed class VisitState
        {
            internal List<Func<Task>> Pending { get; } = new();

            internal List<Action> Writes { get; } = new();
        }

        // Options for a visit. The transforms are required; the limit defaults to one at a time.
        internal sealed class VisitOptions
        {
            internal VisitOptions(PayloadVisitor transformPayload, PayloadsVisitor transformPayloads)
            {
                TransformPayload = transformPayload;
                TransformPayloads = transformPayloads;
            }

            internal PayloadVisitor TransformPayload { get; }

            internal PayloadsVisitor TransformPayloads { get; }

            // How many payloads the walk transforms at once. One (the default) runs them serially,
            // which is what codecs need: a codec may be stateful and, in a workflow, runs in the
            // single-threaded workflow context. External storage raises this to overlap its I/O.
            internal int MaxConcurrency { get; init; } = 1;

            internal CancellationToken CancellationToken { get; init; }
        }

        // Handed to the generated walkers. Wraps each transform with the shared concurrency gate and a
        // shared cancel-on-error signal, and descends into System Nexus envelopes transparently.
        internal sealed class WalkEnv
        {
            private readonly PayloadVisitor transformPayload;
            private readonly PayloadsVisitor transformPayloads;
            private readonly SemaphoreSlim? gate;
            private readonly CancellationTokenSource cancellationSource;

            internal WalkEnv(
                PayloadVisitor transformPayload,
                PayloadsVisitor transformPayloads,
                SemaphoreSlim? gate,
                CancellationTokenSource cancellationSource)
            {
                this.transformPayload = transformPayload;
                this.transformPayloads = transformPayloads;
                this.gate = gate;
                this.cancellationSource = cancellationSource;
            }

            // No gate means a serial walk, which the drain runs one transform at a time.
            internal bool Sequential => gate is null;

            internal Task<Payload> VisitPayloadAsync(Payload payload)
            {
                if (SystemNexusPayloads.IsSystemPayload(payload))
                {
                    if (!TryGetEnvelopeWalk(payload, out var messageType, out var walk))
                    {
                        throw new InvalidOperationException(
                            $"Unrecognized marked System Nexus envelope message type: {messageType}");
                    }

                    // Descended outside the limit so the inner transforms, not this frame, hold slots.
                    return walk!(payload, this);
                }

                return RunLimitedAsync(() => transformPayload(payload, cancellationSource.Token));
            }

            internal async Task<IReadOnlyCollection<Payload>> VisitPayloadsAsync(
                IReadOnlyCollection<Payload> payloads)
            {
                if (!payloads.Any(SystemNexusPayloads.IsSystemPayload))
                {
                    return await RunLimitedAsync(
                        () => transformPayloads(payloads, cancellationSource.Token)).ConfigureAwait(false);
                }

                // Envelopes are descended individually and split the batch around them, keeping the
                // order even though a transform may return a different count than it was given.
                var visited = new List<Payload>();
                var run = new List<Payload>();
                foreach (var payload in payloads)
                {
                    if (SystemNexusPayloads.IsSystemPayload(payload))
                    {
                        if (run.Count > 0)
                        {
                            visited.AddRange(await RunLimitedAsync(
                                () => transformPayloads(run, cancellationSource.Token))
                                .ConfigureAwait(false));
                            run = new List<Payload>();
                        }

                        visited.Add(await VisitPayloadAsync(payload).ConfigureAwait(false));
                    }
                    else
                    {
                        run.Add(payload);
                    }
                }

                if (run.Count > 0)
                {
                    visited.AddRange(await RunLimitedAsync(
                        () => transformPayloads(run, cancellationSource.Token))
                        .ConfigureAwait(false));
                }

                return visited;
            }

            private async Task<TResult> RunLimitedAsync<TResult>(Func<Task<TResult>> work)
            {
                cancellationSource.Token.ThrowIfCancellationRequested();
                if (gate is not null)
                {
                    await gate.WaitAsync(cancellationSource.Token).ConfigureAwait(false);
                }

                try
                {
                    return await work().ConfigureAwait(false);
                }
                catch
                {
                    cancellationSource.Cancel();
                    throw;
                }
                finally
                {
                    gate?.Release();
                }
            }
        }
    }
}
