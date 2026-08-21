namespace Temporalio.Tests.Extensions.OpenTelemetry;

using Temporalio.Client.Interceptors;

internal sealed class NoopClientInterceptor : IClientInterceptor
{
    public ClientOutboundInterceptor InterceptClient(
        ClientOutboundInterceptor nextInterceptor) => nextInterceptor;
}
