using System;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Outbound interceptor to intercept activity calls coming from activities.
    /// </summary>
    public abstract class ActivityOutboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityOutboundInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        protected ActivityOutboundInterceptor(ActivityOutboundInterceptor next)
        {
            MaybeNext = next;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityOutboundInterceptor"/> class.
        /// </summary>
        private protected ActivityOutboundInterceptor()
        {
        }

        /// <summary>
        /// Gets the next interceptor in the chain.
        /// </summary>
        protected ActivityOutboundInterceptor Next =>
            MaybeNext ?? throw new InvalidOperationException("No next interceptor");

        /// <summary>
        /// Gets the next interceptor in the chain if any.
        /// </summary>
        private protected ActivityOutboundInterceptor? MaybeNext { get; init; }

        /// <summary>
        /// Intercept heartbeat.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        public virtual void Heartbeat(HeartbeatInput input)
        {
            Next.Heartbeat(input);
        }
    }
}