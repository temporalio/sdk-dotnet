#pragma warning disable CA1031 // We do want to catch _all_ exceptions in this file sometimes
#pragma warning disable CA1001 // Cancellation source lives for the lifetime of this short-lived driver

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Temporalio.Extensions.WorkflowStreams.Internal
{
    /// <summary>
    /// The shared long-poll engine behind both subscription APIs. It is a single async loop, so
    /// no thread is occupied while a poll is blocked on the server and many subscriptions can run
    /// on the thread pool at once.
    /// </summary>
    /// <remarks>Internal to the workflow streams module; construct subscriptions through
    /// <see cref="WorkflowStreamClient" /> instead.</remarks>
    internal sealed class SubscriptionDriver
    {
        private readonly ITemporalClient client;
        private readonly string workflowId;
        private readonly WorkflowHandle latestRunHandle;
        private readonly List<string> topics;
        private readonly TimeSpan pollCooldown;
        private readonly WorkflowStreamListener listener;
        private readonly Action<SubscriptionDriver> onFinish;
        private readonly TaskCompletionSource<object?> doneTcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly CancellationTokenSource closeCts = new();

        // The run the most recent poll's update was admitted to. Captured before waiting for the
        // update's outcome so that, if that run continues-as-new mid-poll (failing the outcome),
        // we still know which run to inspect to tell a rollover apart from a terminal end.
        private string polledRunId = string.Empty;
        private long offset;
        private volatile bool closed;
        private int started;
        private int finished;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionDriver"/> class.
        /// </summary>
        /// <param name="client">Temporal client to poll through.</param>
        /// <param name="workflowId">Workflow ID hosting the stream.</param>
        /// <param name="options">Subscribe options; the topics list is copied.</param>
        /// <param name="listener">Listener receiving the items.</param>
        /// <param name="onFinish">Callback invoked once when the driver finishes, before the
        /// listener callback and before <see cref="Completion" /> completes.</param>
        public SubscriptionDriver(
            ITemporalClient client,
            string workflowId,
            SubscribeOptions options,
            WorkflowStreamListener listener,
            Action<SubscriptionDriver> onFinish)
        {
            this.client = client;
            this.workflowId = workflowId;
            latestRunHandle = client.GetWorkflowHandle(workflowId);
            topics = options.Topics == null ? new List<string>() : new List<string>(options.Topics);
            offset = options.FromOffset;
            pollCooldown = options.PollCooldown;
            this.listener = listener;
            this.onFinish = onFinish;
        }

        /// <summary>
        /// Gets a task completing when the subscription ends: normally on a clean end or close,
        /// faulted with the failure reported to the listener's OnError.
        /// </summary>
        internal Task Completion => doneTcs.Task;

        /// <summary>
        /// Starts the poll loop. Idempotent.
        /// </summary>
        internal void Start()
        {
            if (Interlocked.CompareExchange(ref started, 1, 0) == 0)
            {
                // RunAsync never throws; every failure routes to FinishError.
                _ = RunAsync();
            }
        }

        /// <summary>
        /// Stops the subscription, interrupting an in-flight poll. Completes Completion normally
        /// without calling OnCompleted. Idempotent.
        /// </summary>
        internal void Close()
        {
            closed = true;
            closeCts.Cancel();
            FinishSilent();
        }

        private async Task RunAsync()
        {
            try
            {
                while (true)
                {
                    if (closed)
                    {
                        FinishSilent();
                        return;
                    }
                    PollResult result;
                    try
                    {
                        // Wait only for Accepted so StartUpdateAsync returns the handle (and its
                        // run ID) as soon as the update is admitted; GetResultAsync then waits
                        // for the outcome. With a Completed wait stage a mid-poll
                        // continue-as-new would fail StartUpdateAsync without a handle, losing
                        // the run ID. There is intentionally no RPC timeout: this is the long
                        // poll, but closing the subscription cancels its RPC.
                        var handle = await latestRunHandle.StartUpdateAsync<PollResult>(
                            WorkflowStreamConstants.PollUpdateName,
                            new object?[]
                            {
                                new PollInput { Topics = new List<string>(topics), FromOffset = offset },
                            },
                            new WorkflowUpdateStartOptions(WorkflowUpdateStage.Accepted)
                            {
                                Rpc = new RpcOptions { CancellationToken = closeCts.Token },
                            }).ConfigureAwait(false);
                        polledRunId = handle.WorkflowRunId ?? string.Empty;
                        result = await handle.GetResultAsync(
                            new RpcOptions { CancellationToken = closeCts.Token }).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (await HandleErrorAsync(e).ConfigureAwait(false))
                        {
                            continue;
                        }
                        return;
                    }

                    // The offset advances before delivery: items are considered consumed once
                    // fetched, matching the other SDKs.
                    offset = result.NextOffset;
                    if (result.Items != null)
                    {
                        foreach (var wireItem in result.Items)
                        {
                            if (closed)
                            {
                                FinishSilent();
                                return;
                            }
                            var item = new WorkflowStreamItem(
                                wireItem.Topic,
                                PayloadWire.Decode(wireItem.Data ?? string.Empty),
                                wireItem.Offset);
                            Task? stage;
                            try
                            {
                                stage = listener.OnNextAsync(item);
                            }
                            catch (Exception e)
                            {
                                FinishError(e);
                                return;
                            }
                            if (stage != null)
                            {
                                try
                                {
                                    await stage.ConfigureAwait(false);
                                }
                                catch (Exception e)
                                {
                                    FinishError(e);
                                    return;
                                }
                            }
                        }
                    }
                    if (closed)
                    {
                        FinishSilent();
                        return;
                    }
                    if (!result.MoreReady && pollCooldown > TimeSpan.Zero)
                    {
                        await Task.Delay(pollCooldown, closeCts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                FinishError(e);
            }
        }

        /// <summary>
        /// Classifies a poll failure. Returns true to retry the poll, false once the subscription
        /// has been finished (silently, cleanly, or with an error).
        /// </summary>
        private async Task<bool> HandleErrorAsync(Exception e)
        {
            if (closed)
            {
                FinishSilent();
                return false;
            }
            ApplicationFailureException? appFailure = null;
            for (var ex = e; ex != null; ex = ex.InnerException)
            {
                if (ex is ApplicationFailureException afe)
                {
                    appFailure = afe;
                    break;
                }
            }
            if (appFailure != null)
            {
                if (appFailure.ErrorType == WorkflowStreamConstants.ErrorTypeTruncatedOffset)
                {
                    // Fell behind truncation; restart from the beginning of whatever still
                    // exists, with an immediate repoll.
                    offset = 0;
                    return true;
                }
                if (appFailure.ErrorType == WorkflowStreamConstants.ErrorTypeStreamDraining)
                {
                    // The workflow is detaching for continue-as-new. Back off and retry; the
                    // poll lands on the successor run once the rollover completes (or the
                    // chain/terminal checks below fire on a genuine end).
                    if (pollCooldown > TimeSpan.Zero)
                    {
                        await Task.Delay(pollCooldown, closeCts.Token).ConfigureAwait(false);
                    }
                    return true;
                }
            }

            // The workflow may have continued-as-new or completed between polls. Describe the run
            // the most recent poll was admitted to: a rolled-over run is closed with status
            // ContinuedAsNew, whereas the latest run would report Running, so describing by run
            // ID is what makes the rollover check fire. The successor run ID is not needed —
            // subsequent polls address the latest run automatically. Follow the chain, exit
            // cleanly on a terminal state, otherwise surface the error.
            WorkflowExecutionStatus status;
            try
            {
                var handle = string.IsNullOrEmpty(polledRunId)
                    ? latestRunHandle
                    : client.GetWorkflowHandle(workflowId, polledRunId);
                status = (await handle.DescribeAsync(new WorkflowDescribeOptions
                {
                    Rpc = new RpcOptions { CancellationToken = closeCts.Token },
                }).ConfigureAwait(false)).Status;
            }
            catch (Exception)
            {
                status = WorkflowExecutionStatus.Unspecified;
            }
            if (status == WorkflowExecutionStatus.ContinuedAsNew)
            {
                return true;
            }
            if (status == WorkflowExecutionStatus.Completed ||
                status == WorkflowExecutionStatus.Failed ||
                status == WorkflowExecutionStatus.Canceled ||
                status == WorkflowExecutionStatus.Terminated ||
                status == WorkflowExecutionStatus.TimedOut)
            {
                FinishCompleted();
                return false;
            }
            FinishError(e);
            return false;
        }

        private void FinishSilent()
        {
            if (Interlocked.Exchange(ref finished, 1) != 0)
            {
                return;
            }
            onFinish(this);
            doneTcs.TrySetResult(null);
        }

        private void FinishCompleted()
        {
            if (Interlocked.Exchange(ref finished, 1) != 0)
            {
                return;
            }
            onFinish(this);
            try
            {
                listener.OnCompleted();
            }
            catch (Exception)
            {
                // Documented: a throwing OnCompleted does not change the outcome.
            }
            doneTcs.TrySetResult(null);
        }

        private void FinishError(Exception e)
        {
            if (Interlocked.Exchange(ref finished, 1) != 0)
            {
                return;
            }
            onFinish(this);
            try
            {
                listener.OnError(e);
            }
            catch (Exception)
            {
                // Documented: a throwing OnError does not change the outcome.
            }
            doneTcs.TrySetException(e);
        }
    }
}
