using System;
using System.Collections.Generic;
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
    /// <summary>External publisher and subscriber for a workflow-hosted stream.</summary>
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
        private readonly StreamPublisher publisher;
        private readonly Dictionary<string, TopicHandle> topicHandles = new();
        private readonly CancellationTokenSource disposeSource = new();
        private readonly CancellationToken disposeToken;
        private Task? disposeTask;

        /// <summary>Initializes a new instance of the <see cref="WorkflowStreamClient"/> class.</summary>
        /// <param name="client">Temporal client used for signals, updates, and queries.</param>
        /// <param name="workflowId">Target workflow ID.</param>
        /// <param name="options">Client options, snapshotted by this constructor.</param>
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
            publisher = new(SignalAsync, payloadConverter, this.options);
        }

        /// <summary>Gets a value indicating whether the publisher owns a live timer.</summary>
        internal bool HasLivePublisherTimer => publisher.HasLiveTimer;

        /// <summary>Creates a stream client targeting the current activity's parent workflow.</summary>
        /// <param name="options">Client options.</param>
        /// <returns>A client for the activity's parent workflow.</returns>
        public static WorkflowStreamClient FromActivity(WorkflowStreamClientOptions? options = null)
        {
            var context = ActivityExecutionContext.Current;
            var id = context.Info.WorkflowId ?? throw new InvalidOperationException(
                "WorkflowStreamClient.FromActivity requires a workflow activity");
            return new(context.TemporalClient, id, options, context.PayloadConverter);
        }

        /// <summary>Gets a memoized handle for a topic.</summary>
        /// <param name="name">Topic name. Null is represented by the empty topic.</param>
        /// <returns>A topic handle.</returns>
        public TopicHandle Topic(string? name)
        {
            name ??= string.Empty;
            lock (stateLock)
            {
                if (!topicHandles.TryGetValue(name, out var handle))
                {
                    handle = new(this, name);
                    topicHandles.Add(name, handle);
                }
                return handle;
            }
        }

        /// <summary>Creates a reusable subscription with independent state per enumeration.</summary>
        /// <param name="options">Subscription options, snapshotted by this call.</param>
        /// <returns>A reusable asynchronous stream of raw Temporal payloads.</returns>
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

        /// <summary>Flushes publications buffered before this call and waits for acknowledgement.</summary>
        /// <param name="cancellationToken">Cancellation token for the flush operation.</param>
        /// <returns>A task that completes when the flush barrier is acknowledged.</returns>
        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            publisher.FlushAsync(cancellationToken);

        /// <summary>Queries the current global offset.</summary>
        /// <param name="cancellationToken">Cancellation token for the query.</param>
        /// <returns>The offset immediately after the last retained item.</returns>
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

        /// <summary>Stops owned subscriptions and drains buffered publications.</summary>
        /// <returns>A value task shared by concurrent disposal calls.</returns>
        public ValueTask DisposeAsync()
        {
            lock (stateLock)
            {
                if (disposeTask == null)
                {
                    disposeSource.Cancel();
                    disposeTask = DisposeCoreAsync(publisher.DisposeAsync().AsTask());
                }
                return new(disposeTask);
            }
        }

        /// <summary>Keeps topic handles on the publisher's single conversion and batching path.</summary>
        /// <param name="topic">Normalized topic name.</param>
        /// <param name="value">Value or raw payload to publish.</param>
        /// <param name="forceFlush">Whether to flush the pending batch immediately.</param>
        internal void Publish(string topic, object? value, bool forceFlush) =>
            publisher.Publish(topic, value, forceFlush);

        private Task SignalAsync(PublishInput input, CancellationToken cancellationToken) =>
            workflowHandle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName,
                new object?[] { input },
                new()
                {
                    Rpc = new() { CancellationToken = cancellationToken },
                });

        private async Task DisposeCoreAsync(Task publisherDisposeTask)
        {
            await Task.Yield();
            try
            {
#pragma warning disable VSTHRD003 // This is the publisher's shared terminal task.
                await publisherDisposeTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
            finally
            {
                disposeSource.Dispose();
            }
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
                        options.Topics, offset, cancellationToken, rpcToken).ConfigureAwait(false);
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
                        if (!await DelayAsync(
                            options.PollCooldown, cancellationToken, rpcToken).ConfigureAwait(false))
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
                            attempt?.RunId, cancellationToken, rpcToken).ConfigureAwait(false);
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
                    !await DelayAsync(
                        options.PollCooldown, cancellationToken, rpcToken).ConfigureAwait(false))
                {
                    yield break;
                }
            }
        }

        private async Task<PollAttempt> PollOnceAsync(
            IReadOnlyCollection<string> topics,
            long offset,
            CancellationToken cancellationToken,
            CancellationToken rpcToken)
        {
            var updateId = Guid.NewGuid().ToString("N");
            WorkflowUpdateHandle<PollResult> updateHandle;
            while (true)
            {
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
                                CancellationToken = rpcToken,
                                Timeout = options.RpcTimeout,
                            },
                        }).ConfigureAwait(false);
                    break;
                }
                catch (WorkflowUpdateRpcTimeoutOrCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (disposeToken.IsCancellationRequested)
                    {
                        disposeToken.ThrowIfCancellationRequested();
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
                try
                {
                    var result = await updateHandle.GetResultAsync(new()
                    {
                        CancellationToken = rpcToken,
                        Timeout = options.RpcTimeout,
                    }).ConfigureAwait(false);
                    return new(result, runId, null);
                }
                catch (WorkflowUpdateRpcTimeoutOrCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (disposeToken.IsCancellationRequested)
                    {
                        disposeToken.ThrowIfCancellationRequested();
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
            CancellationToken cancellationToken,
            CancellationToken rpcToken)
        {
            try
            {
                var description = await client.GetWorkflowHandle(workflowId, runId).DescribeAsync(
                    new()
                    {
                        Rpc = new()
                        {
                            CancellationToken = rpcToken,
                            Timeout = options.RpcTimeout,
                        },
                    }).ConfigureAwait(false);
                return description.Status;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposeToken.IsCancellationRequested)
                {
                    throw;
                }
                return WorkflowExecutionStatus.Unspecified;
            }
            catch (RpcException err) when (
                err.Code == RpcException.StatusCode.DeadlineExceeded ||
                err.Code == RpcException.StatusCode.Cancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposeToken.IsCancellationRequested)
                {
                    disposeToken.ThrowIfCancellationRequested();
                }
                return WorkflowExecutionStatus.Unspecified;
            }
        }

        private async Task<bool> DelayAsync(
            TimeSpan delay,
            CancellationToken cancellationToken,
            CancellationToken rpcToken)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, rpcToken).ConfigureAwait(false);
                }
                return !disposeToken.IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposeToken.IsCancellationRequested)
                {
                    return false;
                }
                throw;
            }
        }

        private sealed class PollAttempt
        {
            private readonly PollResult? result;

            /// <summary>Initializes a new instance of the <see cref="PollAttempt"/> class.</summary>
            /// <param name="result">Successful result, if present.</param>
            /// <param name="runId">Admitted workflow run ID, if known.</param>
            /// <param name="error">Poll error, if present.</param>
            internal PollAttempt(PollResult? result, string? runId, Exception? error)
            {
                this.result = result;
                RunId = runId;
                Error = error;
            }

            /// <summary>Gets the successful result, if present.</summary>
            internal PollResult? Result => result;

            /// <summary>Gets the admitted workflow run ID, if known.</summary>
            internal string? RunId { get; }

            /// <summary>Gets the poll error, if present.</summary>
            internal Exception? Error { get; }
        }
    }
}
