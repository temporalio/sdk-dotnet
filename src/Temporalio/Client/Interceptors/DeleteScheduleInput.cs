namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.DeleteScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="RpcOptions">RPC options.</param>
    public record DeleteScheduleInput(
        string ID,
        RpcOptions? RpcOptions);
}