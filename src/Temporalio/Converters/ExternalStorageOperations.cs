using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Worker;

namespace Temporalio.Converters
{
    /// <summary>
    /// The external storage passes run over the payloads exchanged with the server: the runner's
    /// store and retrieve, combined with the payload codec passes over the references.
    /// </summary>
    internal static class ExternalStorageOperations
    {
        /// <summary>
        /// Store the payloads in a message, then encode and mark the references that replace them
        /// when <see cref="ExternalStorage.EncodeReferences"/> is set.
        /// </summary>
        /// <typeparam name="T">The message type being visited.</typeparam>
        /// <param name="runner">Runner to store through.</param>
        /// <param name="walk">
        /// The generated <c>PayloadVisitors.Walk</c> overload for <typeparamref name="T"/>, passed
        /// as a method group.
        /// </param>
        /// <param name="message">The message to visit.</param>
        /// <param name="initialTarget">
        /// Execution the stored payloads are offloaded on behalf of, or null when none is known,
        /// until <paramref name="deriveTarget"/> replaces it.
        /// </param>
        /// <param name="deriveTarget">
        /// Given each message walked and its enclosing target, returns the target for that
        /// message's payloads, where null means no target. Passing no callback keeps
        /// <paramref name="initialTarget"/> throughout.
        /// </param>
        /// <param name="codec">
        /// Codec that encoded the message's payloads, or null when none is configured.
        /// </param>
        /// <param name="deriveCodec">
        /// Given each message walked and its enclosing codec, returns the codec for that message's
        /// payloads. Passing no callback keeps <paramref name="codec"/> throughout.
        /// </param>
        /// <param name="metrics">
        /// Accumulates what this pass stored, or null to not measure it.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the message has been visited.</returns>
        internal static async Task StoreAsync<T>(
            IExternalStorageRunner runner,
            Action<T, PayloadVisitors.WalkEnv, PayloadVisitors.VisitState> walk,
            T message,
            IStorageDriverTargetInfo? initialTarget,
            Func<IMessage, IStorageDriverTargetInfo?, IStorageDriverTargetInfo?>? deriveTarget,
            IPayloadCodec? codec,
            Func<IMessage, IPayloadCodec?, IPayloadCodec?>? deriveCodec,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken)
        {
            await runner.StoreAsync(walk, message, initialTarget, deriveTarget, metrics, cancellationToken)
                .ConfigureAwait(false);
            if (runner.Storage?.EncodeReferences == true)
            {
                await ExternalStorageReferenceCodec.EncodeAsync(
                    walk, message, codec, deriveCodec, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Store the given payloads, then encode and mark the references that replace them when
        /// <see cref="ExternalStorage.EncodeReferences"/> is set.
        /// </summary>
        /// <param name="runner">Runner to store through.</param>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="target">
        /// Execution the stored payloads are offloaded on behalf of, or null when none is known.
        /// </param>
        /// <param name="codec">Codec that encoded the payloads, or null when none is configured.</param>
        /// <param name="metrics">Accumulates what was stored, or null to not measure it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resulting payloads.</returns>
        internal static async Task<IReadOnlyCollection<Payload>> StoreAsync(
            IExternalStorageRunner runner,
            IReadOnlyCollection<Payload> payloads,
            IStorageDriverTargetInfo? target,
            IPayloadCodec? codec,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken)
        {
            var stored = await runner.StoreAsync(payloads, target, metrics, cancellationToken)
                .ConfigureAwait(false);
            return runner.Storage?.EncodeReferences == true
                ? await ExternalStorageReferenceCodec.EncodeAsync(stored, codec).ConfigureAwait(false)
                : stored;
        }

        /// <summary>
        /// Decode the marked references in a message, then retrieve every reference.
        /// </summary>
        /// <typeparam name="T">The message type being visited.</typeparam>
        /// <param name="runner">Runner to retrieve through.</param>
        /// <param name="walk">
        /// The generated <c>PayloadVisitors.Walk</c> overload for <typeparamref name="T"/>, passed
        /// as a method group.
        /// </param>
        /// <param name="message">The message to visit.</param>
        /// <param name="codec">
        /// Codec that decodes the message's payloads, or null when none is configured.
        /// </param>
        /// <param name="deriveCodec">
        /// Given each message walked and its enclosing codec, returns the codec for that message's
        /// payloads. Passing no callback keeps <paramref name="codec"/> throughout.
        /// </param>
        /// <param name="metrics">
        /// Accumulates what this pass retrieved, or null to not measure it.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the message has been visited.</returns>
        internal static async Task RetrieveAsync<T>(
            IExternalStorageRunner runner,
            Action<T, PayloadVisitors.WalkEnv, PayloadVisitors.VisitState> walk,
            T message,
            IPayloadCodec? codec,
            Func<IMessage, IPayloadCodec?, IPayloadCodec?>? deriveCodec,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken)
        {
            // Without storage, retrieval rejects any reference, so decoding one first would only
            // add codec calls.
            if (runner.Storage is null || await ExternalStorageReferenceCodec.DecodeAsync(
                walk, message, codec, deriveCodec, cancellationToken).ConfigureAwait(false))
            {
                await runner.RetrieveAsync(walk, message, metrics, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Decode the marked references in the given payloads, then retrieve every reference.
        /// </summary>
        /// <param name="runner">Runner to retrieve through.</param>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="codec">Codec that decodes the payloads, or null when none is configured.</param>
        /// <param name="metrics">Accumulates what was retrieved, or null to not measure it.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resulting payloads.</returns>
        internal static async Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            IExternalStorageRunner runner,
            IReadOnlyCollection<Payload> payloads,
            IPayloadCodec? codec,
            StorageOperationMetricsBuilder? metrics,
            CancellationToken cancellationToken)
        {
            if (runner.Storage is null)
            {
                return await runner.RetrieveAsync(payloads, metrics, cancellationToken)
                    .ConfigureAwait(false);
            }
            var (decoded, anyReference) = await ExternalStorageReferenceCodec.DecodeAsync(
                payloads, codec).ConfigureAwait(false);
            return anyReference
                ? await runner.RetrieveAsync(decoded, metrics, cancellationToken).ConfigureAwait(false)
                : decoded;
        }
    }
}
