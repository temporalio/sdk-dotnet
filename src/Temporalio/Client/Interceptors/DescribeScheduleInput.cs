namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.DescribeScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="RpcOptions">RPC options.</param>
    public record DescribeScheduleInput(
        string ID,
        RpcOptions? RpcOptions);
}