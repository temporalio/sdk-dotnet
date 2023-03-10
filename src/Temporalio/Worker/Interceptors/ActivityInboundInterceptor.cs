using System;
using System.Threading.Tasks;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Inbound interceptor to intercept activity calls coming from the server.
    /// </summary>
    public abstract class ActivityInboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityInboundInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        protected ActivityInboundInterceptor(ActivityInboundInterceptor next) => MaybeNext = next;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityInboundInterceptor"/> class.
        /// </summary>
        private protected ActivityInboundInterceptor()
        {
        }

        /// <summary>
        /// Gets the next interceptor in the chain.
        /// </summary>
        protected ActivityInboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        /// <summary>
        /// Gets the next interceptor in the chain if any.
        /// </summary>
        private protected ActivityInboundInterceptor? MaybeNext { get; init; }

        /// <summary>
        /// Initialize with an outbound interceptor.
        /// </summary>
        /// <param name="outbound">Outbound interceptor to initialize with.</param>
        /// <remarks>
        /// To add a custom outbound interceptor, wrap the given outbound before sending to the
        /// next "Init" call.
        /// </remarks>
        public virtual void Init(ActivityOutboundInterceptor outbound) => Next.Init(outbound);

        /// <summary>
        /// Intercept activity execution.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completed activity result.</returns>
        public virtual Task<object?> ExecuteActivityAsync(ExecuteActivityInput input) =>
            Next.ExecuteActivityAsync(input);
    }
}