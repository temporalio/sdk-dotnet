
namespace Temporalio.Client.Interceptors
{

    public interface IClientInterceptor
    {
        #if NETCOREAPP3_0_OR_GREATER
        ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next)
        {
            return next;
        }
        #else
        ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next);
        #endif
    }
}