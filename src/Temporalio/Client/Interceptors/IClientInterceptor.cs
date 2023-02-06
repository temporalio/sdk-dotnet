namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Interceptor for client calls.
    /// </summary>
    public interface IClientInterceptor
    {
        /// <summary>
        /// Create a client outbound interceptor to intercept calls.
        /// </summary>
        /// <param name="nextInterceptor">The next interceptor in the chain to call.</param>
        /// <returns>Created interceptor.</returns>
#if NETCOREAPP3_0_OR_GREATER
        ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor)
        {
            return nextInterceptor;
        }
#else
        ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor);
#endif
    }
}