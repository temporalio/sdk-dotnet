using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// External publisher and subscriber for a workflow-hosted stream.
    /// </summary>
    /// <remarks>
    /// Dispose asynchronously to drain buffered publications and stop subscriptions owned by this
    /// client. WARNING: Workflow Streams is experimental and may change.
    /// </remarks>
    public sealed class WorkflowStreamClient : IAsyncDisposable
    {
        private readonly object stateLock = new();
        private readonly ITemporalClient client;
        private readonly string workflowId;
        private readonly WorkflowHandle workflowHandle;
        private readonly WorkflowStreamClientOptions options;
        private readonly IPayloadConverter payloadConverter;
        private readonly StreamPublisher publisher;
        private readonly CancellationTokenSource disposeSource = new();
        private readonly CancellationToken disposeToken;
        private Task? disposeTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStreamClient"/> class.
        /// </summary>
        /// <param name="client">
        /// Temporal client used for signals, updates, and queries.
        /// </param>
        /// <param name="workflowId">
        /// Target workflow ID.
        /// </param>
        /// <param name="options">
        /// Client options, snapshotted by this constructor.
        /// </param>
        public WorkflowStreamClient(
            ITemporalClient client,
            string workflowId,
            WorkflowStreamClientOptions? options = null)
            : this(client, workflowId, options, null)
        {
        }

        private WorkflowStreamClient(
            ITemporalClient client,
            string workflowId,
            WorkflowStreamClientOptions? options,
            IPayloadConverter? payloadConverter)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.workflowId = workflowId ?? throw new ArgumentNullException(nameof(workflowId));
            this.options = (WorkflowStreamClientOptions)(options ??
                new WorkflowStreamClientOptions()).Clone();
            if (this.options.BatchInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "BatchInterval must be greater than zero");
            }
            if (this.options.MaxBatchSize < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "MaxBatchSize cannot be negative");
            }
            if (this.options.MaxRetryDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "MaxRetryDuration must be greater than zero");
            }
            if (this.options.RpcTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "The RPC timeout must be greater than zero");
            }
            workflowHandle = client.GetWorkflowHandle(workflowId);
            disposeToken = disposeSource.Token;
            payloadConverter ??= client.Options.DataConverter.WithSerializationContext(
                new ISerializationContext.Workflow(client.Options.Namespace, workflowId)).PayloadConverter;
            this.payloadConverter = payloadConverter;
            publisher = new(SignalAsync, this.payloadConverter, this.options);
        }

        /// <summary>
        /// Gets a value indicating whether the publisher owns a live timer.
        /// </summary>
        internal bool HasLivePublisherTimer => publisher.HasLiveTimer;

        /// <summary>
        /// Creates a stream client targeting the current activity's parent workflow.
        /// </summary>
        /// <param name="options">
        /// Client options.
        /// </param>
        /// <returns>
        /// A client for the activity's parent workflow.
        /// </returns>
        /// <remarks>
        /// The activity's payload converter is used for stream items. Payload converters that
        /// require matching serialization context during deserialization are not compatible with
        /// subscriptions created from this client because published and received items have
        /// different serialization contexts.
        /// </remarks>
        public static WorkflowStreamClient FromActivity(WorkflowStreamClientOptions? options = null)
        {
            var context = ActivityExecutionContext.Current;
            var id = context.Info.WorkflowId ?? throw new InvalidOperationException(
                "WorkflowStreamClient.FromActivity requires a workflow activity");
            return new(context.TemporalClient, id, options, context.PayloadConverter);
        }

        /// <summary>
        /// Creates a handle for a topic.
        /// </summary>
        /// <param name="name">
        /// Topic name. Null is represented by the empty topic.
        /// </param>
        /// <returns>
        /// A topic handle.
        /// </returns>
        public WorkflowStreamClientTopicHandle GetTopic(string? name)
        {
            name ??= string.Empty;
            return new(this, name);
        }

        /// <summary>
        /// Creates a strongly typed handle for a topic.
        /// </summary>
        /// <typeparam name="T">
        /// Type of values published to and received from the topic.
        /// </typeparam>
        /// <param name="name">
        /// Topic name. Null is represented by the empty topic.
        /// </param>
        /// <returns>
        /// A topic handle.
        /// </returns>
        public WorkflowStreamClientTopicHandle<T> GetTopic<T>(string? name)
        {
            name ??= string.Empty;
            return new(this, name);
        }

        /// <summary>
        /// Creates a reusable subscription with independent state per enumeration.
        /// </summary>
        /// <param name="options">
        /// Subscription options, snapshotted by this call.
        /// </param>
        /// <returns>
        /// A reusable asynchronous stream of raw Temporal payloads.
        /// </returns>
        public IAsyncEnumerable<WorkflowStreamItem> SubscribeAsync(
            WorkflowStreamSubscribeOptions? options = null)
        {
            var snapshot = (WorkflowStreamSubscribeOptions)(options ??
                new WorkflowStreamSubscribeOptions()).Clone();
            if (snapshot.FromOffset < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "FromOffset cannot be negative");
            }
            if (snapshot.PollCooldown < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), "PollCooldown cannot be negative");
            }
            snapshot.Topics = snapshot.Topics.Select(topic => topic ?? string.Empty).ToArray();
            return SubscribeCoreAsync(snapshot);
        }

        /// <summary>
        /// Creates a reusable, strongly typed subscription.
        /// </summary>
        /// <typeparam name="T">
        /// Type to which each item is deserialized.
        /// </typeparam>
        /// <param name="options">
        /// Subscription options, snapshotted by this call.
        /// </param>
        /// <returns>
        /// A reusable asynchronous stream of decoded values.
        /// </returns>
        public IAsyncEnumerable<WorkflowStreamItem<T>> SubscribeAsync<T>(
            WorkflowStreamSubscribeOptions? options = null) =>
            ConvertItemsAsync<T>(SubscribeAsync(options));

        /// <summary>
        /// Flushes publications buffered before this call and waits for acknowledgement.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancellation token for the flush operation.
        /// </param>
        /// <returns>
        /// A task that completes when the flush barrier is acknowledged.
        /// </returns>
        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            publisher.FlushAsync(cancellationToken);

        /// <summary>
        /// Queries the current global offset.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancellation token for the query.
        /// </param>
        /// <returns>
        /// The offset immediately after the last retained item.
        /// </returns>
        public async Task<long> GetOffsetAsync(CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                disposeToken, cancellationToken);
            return await workflowHandle.QueryAsync<long>(
                WorkflowStreamConstants.OffsetQueryName,
                Array.Empty<object?>(),
                new()
                {
                    Rpc = new() { CancellationToken = linked.Token },
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops owned subscriptions and drains buffered publications.
        /// </summary>
        /// <returns>
        /// A value task shared by concurrent disposal calls.
        /// </returns>
        public ValueTask DisposeAsync()
        {
            TaskCompletionSource<object?>? startSource = null;
            Task task;
            lock (stateLock)
            {
                if (disposeTask == null)
                {
                    startSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    disposeTask = DisposeCoreAsync(startSource.Task);
                }
                task = disposeTask;
            }
            if (startSource != null)
            {
                try
                {
                    disposeSource.Cancel();
                }
                finally
                {
                    startSource.SetResult(null);
                }
            }
            return new(task);
        }

        /// <summary>
        /// Keeps topic handles on the publisher's single conversion and batching path.
        /// </summary>
        /// <param name="topic">
        /// Normalized topic name.
        /// </param>
        /// <param name="value">
        /// Value or raw payload to publish.
        /// </param>
        /// <param name="forceFlush">
        /// Whether to flush the pending batch immediately.
        /// </param>
        internal void Publish(string topic, object? value, bool forceFlush) =>
            publisher.Publish(topic, value, forceFlush);

        private async IAsyncEnumerable<WorkflowStreamItem<T>> ConvertItemsAsync<T>(
            IAsyncEnumerable<WorkflowStreamItem> items,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return new(
                    item.Topic,
                    (T)payloadConverter.ToValue(item.Payload, typeof(T))!,
                    item.Offset);
            }
        }

        private Task SignalAsync(PublishInput input, CancellationToken cancellationToken) =>
            workflowHandle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[] { input },
                new()
                {
                    Rpc = new() { CancellationToken = cancellationToken },
                });

        private async Task DisposeCoreAsync(Task startTask)
        {
#pragma warning disable VSTHRD003 // These tasks coordinate the client's shared disposal operation.
            await startTask.ConfigureAwait(false);
            try
            {
                await publisher.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                disposeSource.Dispose();
            }
#pragma warning restore VSTHRD003
        }

        private async IAsyncEnumerable<WorkflowStreamItem> SubscribeCoreAsync(
            WorkflowStreamSubscribeOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            static ApplicationFailureException? FindApplicationFailure(Exception error)
            {
                for (Exception? current = error; current != null; current = current.InnerException)
                {
                    if (current is ApplicationFailureException failure)
                    {
                        return failure;
                    }
                }
                return null;
            }

            static bool IsTerminal(WorkflowExecutionStatus status) =>
                status == WorkflowExecutionStatus.Completed ||
                status == WorkflowExecutionStatus.Failed ||
                status == WorkflowExecutionStatus.Canceled ||
                status == WorkflowExecutionStatus.Terminated ||
                status == WorkflowExecutionStatus.TimedOut;

            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                disposeToken, cancellationToken);
            var rpcToken = linkedSource.Token;
            var offset = options.FromOffset;
            string? polledRunId = null;

            while (true)
            {
                if (disposeToken.IsCancellationRequested)
                {
                    yield break;
                }
                cancellationToken.ThrowIfCancellationRequested();

                PollAttempt? attempt = null;
                Exception? pollError = null;
                var clientDisposed = false;
                try
                {
                    attempt = await PollOnceAsync(
                        options.Topics, offset, options.PollCooldown, rpcToken).ConfigureAwait(false);
                    if (attempt.RunId != null)
                    {
                        polledRunId = attempt.RunId;
                    }
                    pollError = attempt.Error;
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (disposeToken.IsCancellationRequested)
                    {
                        clientDisposed = true;
                    }
                    else
                    {
                        throw;
                    }
                }
                if (clientDisposed)
                {
                    yield break;
                }
                if (pollError != null)
                {
                    var failure = FindApplicationFailure(pollError);
                    if (failure?.ErrorType == WorkflowStreamConstants.TruncatedOffsetErrorType)
                    {
                        offset = 0;
                        continue;
                    }
                    if (failure?.ErrorType == WorkflowStreamConstants.StreamDrainingErrorType)
                    {
                        if (!await DelayAsync(options.PollCooldown, rpcToken).ConfigureAwait(false))
                        {
                            yield break;
                        }
                        continue;
                    }

                    WorkflowExecutionStatus status = WorkflowExecutionStatus.Unspecified;
                    var describeCanceledByDispose = false;
                    try
                    {
                        status = await DescribeStatusAsync(
                            polledRunId, rpcToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (disposeToken.IsCancellationRequested)
                        {
                            describeCanceledByDispose = true;
                        }
                        else
                        {
                            throw;
                        }
                    }
                    if (describeCanceledByDispose)
                    {
                        yield break;
                    }
                    if (status == WorkflowExecutionStatus.ContinuedAsNew)
                    {
                        continue;
                    }
                    if (IsTerminal(status))
                    {
                        yield break;
                    }
                    throw pollError;
                }

                foreach (var item in attempt!.Result!.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (disposeToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    yield return new(
                        item.Topic ?? string.Empty,
                        PayloadWire.Decode(item.Data),
                        item.Offset);
                }
                offset = attempt!.Result!.NextOffset;
                if (!attempt.Result.MoreReady &&
                    !await DelayAsync(options.PollCooldown, rpcToken).ConfigureAwait(false))
                {
                    yield break;
                }
            }
        }

        private async Task<PollAttempt> PollOnceAsync(
            IReadOnlyCollection<string> topics,
            long offset,
            TimeSpan pollCooldown,
            CancellationToken cancellationToken)
        {
            var updateId = Guid.NewGuid().ToString("N");
            WorkflowUpdateHandle<PollResult> updateHandle;
            while (true)
            {
                var attemptStartedAt = Stopwatch.GetTimestamp();
                try
                {
                    updateHandle = await workflowHandle.StartUpdateAsync<PollResult>(
                        WorkflowStreamConstants.PollUpdateName,
                        new object?[]
                        {
                            new PollInput { Topics = topics, FromOffset = offset },
                        },
                        new(WorkflowUpdateStage.Accepted)
                        {
                            Id = updateId,
                            Rpc = new()
                            {
                                CancellationToken = cancellationToken,
                                Timeout = options.RpcTimeout,
                            },
                        }).ConfigureAwait(false);
                    break;
                }
                catch (WorkflowUpdateRpcTimeoutOrCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attemptDuration = TimeSpan.FromSeconds(
                        (Stopwatch.GetTimestamp() - attemptStartedAt) /
                        (double)Stopwatch.Frequency);
                    if (attemptDuration < TimeSpan.FromTicks(options.RpcTimeout.Ticks / 2))
                    {
                        await Task.Delay(pollCooldown, cancellationToken).
                            ConfigureAwait(false);
                    }
                }
                catch (TemporalException err)
                {
                    return new(null, null, err);
                }
                catch (InvalidOperationException err)
                {
                    return new(null, null, err);
                }
            }

            var runId = updateHandle.WorkflowRunId;
            while (true)
            {
                var attemptStartedAt = Stopwatch.GetTimestamp();
                try
                {
                    var result = await updateHandle.GetResultAsync(new()
                    {
                        CancellationToken = cancellationToken,
                        Timeout = options.RpcTimeout,
                    }).ConfigureAwait(false);
                    return new(result, runId, null);
                }
                catch (WorkflowUpdateRpcTimeoutOrCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attemptDuration = TimeSpan.FromSeconds(
                        (Stopwatch.GetTimestamp() - attemptStartedAt) /
                        (double)Stopwatch.Frequency);
                    if (attemptDuration < TimeSpan.FromTicks(options.RpcTimeout.Ticks / 2))
                    {
                        await Task.Delay(pollCooldown, cancellationToken).
                            ConfigureAwait(false);
                    }
                }
                catch (TemporalException err)
                {
                    return new(null, runId, err);
                }
                catch (InvalidOperationException err)
                {
                    return new(null, runId, err);
                }
            }
        }

        private async Task<WorkflowExecutionStatus> DescribeStatusAsync(
            string? runId,
            CancellationToken cancellationToken)
        {
            try
            {
                var description = await client.GetWorkflowHandle(workflowId, runId).DescribeAsync(
                    new()
                    {
                        Rpc = new()
                        {
                            CancellationToken = cancellationToken,
                            Timeout = options.RpcTimeout,
                        },
                    }).ConfigureAwait(false);
                return description.Status;
            }
#pragma warning disable CA1031 // The original poll failure is more relevant than a status lookup failure.
            catch (Exception)
#pragma warning restore CA1031
            {
                cancellationToken.ThrowIfCancellationRequested();
                return WorkflowExecutionStatus.Unspecified;
            }
        }

        private async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                return !disposeToken.IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                if (disposeToken.IsCancellationRequested)
                {
                    return false;
                }
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
        }

        private sealed class PollAttempt
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PollAttempt"/> class.
            /// </summary>
            /// <param name="result">
            /// Successful result, if present.
            /// </param>
            /// <param name="runId">
            /// Admitted workflow run ID, if known.
            /// </param>
            /// <param name="error">
            /// Poll error, if present.
            /// </param>
            internal PollAttempt(PollResult? result, string? runId, Exception? error)
            {
                Result = result;
                RunId = runId;
                Error = error;
            }

            /// <summary>
            /// Gets the successful result, if present.
            /// </summary>
            internal PollResult? Result { get; }

            /// <summary>
            /// Gets the admitted workflow run ID, if known.
            /// </summary>
            internal string? RunId { get; }

            /// <summary>
            /// Gets the poll error, if present.
            /// </summary>
            internal Exception? Error { get; }
        }
    }
}
