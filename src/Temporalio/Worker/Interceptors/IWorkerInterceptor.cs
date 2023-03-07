using System;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Interceptor for intercepting activities and workflows.
    /// </summary>
    public interface IWorkerInterceptor
    {
        /// <summary>
        /// Gets the optional type of <see cref="WorkflowInboundInterceptor"/> to instantiate for
        /// interception. This type must contain a public constructor accepting a single
        /// <see cref="WorkflowInboundInterceptor"/> parameter for the next interceptor in the
        /// chain.
        /// </summary>
#if NETCOREAPP3_0_OR_GREATER
        Type? WorkflowInboundInterceptorType => null;
#else
        Type? WorkflowInboundInterceptorType { get; }
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
    }

#if NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Generic <see cref="IWorkerInterceptor" /> with workflow inbound interceptor as type
    /// argument.
    /// </summary>
    /// <typeparam name="T">Workflow inbound interceptor type.</typeparam>
    public interface IWorkerInterceptor<T> : IWorkerInterceptor
        where T : WorkflowInboundInterceptor
    {
        /// <inheritdoc />
#pragma warning disable CA1033 // Providing default interface impl shouldn't trigger this
        Type? IWorkerInterceptor.WorkflowInboundInterceptorType => typeof(T);
#pragma warning restore CA1033
    }
#endif
}