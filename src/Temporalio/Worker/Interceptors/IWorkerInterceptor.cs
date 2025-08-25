namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Interceptor for intercepting activities and workflows.
    /// </summary>
    public interface IWorkerInterceptor
    {
        /// <summary>
        /// Create a workflow inbound interceptor to intercept calls.
        /// </summary>
        /// <param name="nextInterceptor">The next interceptor in the chain to call.</param>
        /// <returns>Created interceptor.</returns>
#if NETCOREAPP3_0_OR_GREATER
        WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) =>
            nextInterceptor;
#else
        WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor);
#endif

        /// <summary>
        /// Create an activity inbound interceptor to intercept calls.
        /// </summary>
        /// <param name="nextInterceptor">The next interceptor in the chain to call.</param>
        /// <returns>Created interceptor.</returns>
#if NETCOREAPP3_0_OR_GREATER
        ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) =>
            nextInterceptor;
#else
        ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor);
#endif

        /// <summary>
        /// Create a Nexus operation inbound interceptor to intercept calls.
        /// </summary>
        /// <param name="nextInterceptor">The next interceptor in the chain to call.</param>
        /// <returns>Created interceptor.</returns>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
#if NETCOREAPP3_0_OR_GREATER
        NexusOperationInboundInterceptor InterceptNexusOperation(NexusOperationInboundInterceptor nextInterceptor) =>
            nextInterceptor;
#else
        NexusOperationInboundInterceptor InterceptNexusOperation(NexusOperationInboundInterceptor nextInterceptor);
#endif
    }
}