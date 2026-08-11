using System;
using System.Threading.Tasks;
using Temporalio.Extensions.WorkflowStreams.Internal;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Controls a listener-based subscription started with
    /// <see cref="WorkflowStreamClient.Subscribe(SubscribeOptions, WorkflowStreamListener)" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public sealed class WorkflowStreamSubscriptionHandle : IDisposable
    {
        private readonly SubscriptionDriver driver;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStreamSubscriptionHandle"/> class.
        /// </summary>
        /// <param name="driver">Driver running the subscription.</param>
        internal WorkflowStreamSubscriptionHandle(SubscriptionDriver driver) => this.driver = driver;

        /// <summary>
        /// Gets a task that tracks the end of the subscription: it completes normally when the
        /// stream ends cleanly (after <see cref="WorkflowStreamListener.OnCompleted" />) or the
        /// subscription is disposed, and faults with the failure passed to
        /// <see cref="WorkflowStreamListener.OnError" />.
        /// </summary>
        public Task Completion => driver.Completion;

        /// <summary>
        /// Stops the subscription before the next poll; a poll already blocked on the server is
        /// not interrupted, and its result is discarded. Idempotent. Does not trigger
        /// <see cref="WorkflowStreamListener.OnCompleted" />.
        /// </summary>
        public void Dispose() => driver.Close();
    }
}
