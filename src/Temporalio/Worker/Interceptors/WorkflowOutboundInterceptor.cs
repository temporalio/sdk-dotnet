using System;
using System.Threading.Tasks;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Outbound interceptor to intercept workflow calls coming from workflows.
    /// </summary>
    public abstract class WorkflowOutboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowOutboundInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        protected WorkflowOutboundInterceptor(WorkflowOutboundInterceptor next) => MaybeNext = next;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowOutboundInterceptor"/> class.
        /// </summary>
        private protected WorkflowOutboundInterceptor()
        {
        }

        /// <summary>
        /// Gets the next interceptor in the chain.
        /// </summary>
        protected WorkflowOutboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        /// <summary>
        /// Gets the next interceptor in the chain if any.
        /// </summary>
        private protected WorkflowOutboundInterceptor? MaybeNext { get; init; }

        /// <summary>
        /// Intercept starting a new timer.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Task for completion.</returns>
        public virtual Task DelayAsync(DelayAsyncInput input) => Next.DelayAsync(input);
    }
}