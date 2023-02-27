using System;
using System.Threading.Tasks;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Inbound interceptor class instantiated for every workflow run to intercept workflow calls
    /// coming from the server.
    /// </summary>
    public abstract class WorkflowInboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowInboundInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        protected WorkflowInboundInterceptor(WorkflowInboundInterceptor next)
        {
            MaybeNext = next;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowInboundInterceptor"/> class.
        /// </summary>
        private protected WorkflowInboundInterceptor()
        {
        }

        /// <summary>
        /// Gets the next interceptor in the chain.
        /// </summary>
        protected WorkflowInboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        /// <summary>
        /// Gets the next interceptor in the chain if any.
        /// </summary>
        private protected WorkflowInboundInterceptor? MaybeNext { get; init; }

        /// <summary>
        /// Initialize with an outbound interceptor.
        /// </summary>
        /// <param name="outbound">Outbound interceptor to initialize with.</param>
        /// <remarks>
        /// To add a custom outbound interceptor, wrap the given outbound before sending to the
        /// next "Init" call.
        /// </remarks>
        public virtual void Init(WorkflowOutboundInterceptor outbound)
        {
            Next.Init(outbound);
        }

        /// <summary>
        /// Intercept workflow execution.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completed workflow result.</returns>
        public virtual Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
        {
            return Next.ExecuteWorkflowAsync(input);
        }
    }
}