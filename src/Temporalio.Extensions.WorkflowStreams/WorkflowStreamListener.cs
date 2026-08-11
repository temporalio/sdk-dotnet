using System;
using System.Threading.Tasks;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Receives items from a workflow stream subscription without occupying a caller thread. Pass
    /// one to <see cref="WorkflowStreamClient.Subscribe(SubscribeOptions, WorkflowStreamListener)" />;
    /// the returned <see cref="WorkflowStreamSubscriptionHandle" /> stops the subscription.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Callbacks are serialized (never invoked concurrently) and run on the thread pool, so they
    /// must not block; to defer further delivery, return a pending task from
    /// <see cref="OnNextAsync" />.
    /// </para>
    /// </remarks>
    public abstract class WorkflowStreamListener
    {
        /// <summary>
        /// Called with the next item on the stream. Return
        /// <see cref="Task.CompletedTask" /> (or an already-completed task) to receive the next
        /// item immediately; return a pending task to defer both further delivery and the next
        /// poll until it completes (backpressure). A task that faults — or an exception thrown
        /// directly — stops the subscription and is reported to <see cref="OnError" />. A null
        /// returned task is treated as completed.
        /// </summary>
        /// <param name="item">The next stream item.</param>
        /// <returns>A task governing backpressure, as described above.</returns>
        public abstract Task OnNextAsync(WorkflowStreamItem item);

        /// <summary>
        /// Called once when the subscription stops because of an unrecoverable failure (including
        /// a failure from <see cref="OnNextAsync" />). No further callbacks follow. The default
        /// implementation is a no-op; the failure also faults the handle's
        /// <see cref="WorkflowStreamSubscriptionHandle.Completion" /> task, which is the
        /// programmatic channel for the failure.
        /// </summary>
        /// <param name="failure">The failure that stopped the subscription.</param>
        public virtual void OnError(Exception failure)
        {
        }

        /// <summary>
        /// Called once when the stream ends cleanly because the workflow reached a terminal
        /// state. Not called when the subscription is stopped via
        /// <see cref="IDisposable.Dispose" />. No further callbacks follow.
        /// </summary>
        public virtual void OnCompleted()
        {
        }
    }
}
