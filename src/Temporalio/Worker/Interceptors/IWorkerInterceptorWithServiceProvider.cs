using System;
#if NETCOREAPP3_0_OR_GREATER
using Temporalio.Activities;
#endif

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Interceptor for intercepting activities and workflows providing activity
    /// interceptors with access to the service provider.
    /// </summary>
    public interface IWorkerInterceptorWithServiceProvider : IWorkerInterceptor
    {
        /// <summary>
        /// Create an activity inbound interceptor to intercept calls, with
        /// access to the service provider for the current activity.
        /// </summary>
        /// <param name="serviceProvider">Service provider instance scoped to the current activity.</param>
        /// <param name="nextInterceptor">The next interceptor in the chain to call.</param>
        /// <returns>Created interceptor.</returns>
#if NETCOREAPP3_0_OR_GREATER
        ActivityInboundInterceptor InterceptActivity(IServiceProvider serviceProvider, ActivityInboundInterceptor nextInterceptor) =>
            nextInterceptor;

        ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
            InterceptActivity(ActivityServiceProviderAccessor.Current, nextInterceptor);
#else
        ActivityInboundInterceptor InterceptActivity(IServiceProvider serviceProvider, ActivityInboundInterceptor nextInterceptor);
#endif
    }
}