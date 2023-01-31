namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Interceptor for intercepting activities and workflows.
    /// </summary>
    public interface IWorkerInterceptor
    {
        /// <summary>
        /// Create an activity inbound interceptor to intercept calls.
        /// </summary>
        /// <param name="next">The next interceptor in the chain to call.</param>
        /// <returns>Created interceptor.</returns>
#if NETCOREAPP3_0_OR_GREATER
        ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor next)
        {
            return next;
        }
#else
        ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor next);
#endif
    }
}