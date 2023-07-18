namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.DeleteScheduleAsync" />.
    /// </summary>
    /// <param name="Id">Schedule ID.</param>
    /// <param name="RpcOptions">RPC options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record DeleteScheduleInput(
        string Id,
        RpcOptions? RpcOptions);
}