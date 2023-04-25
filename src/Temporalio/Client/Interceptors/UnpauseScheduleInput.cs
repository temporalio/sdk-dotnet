namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.UnpauseScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="Note">Unpause note.</param>
    /// <param name="RpcOptions">RPC options.</param>
    public record UnpauseScheduleInput(
        string ID,
        string? Note,
        RpcOptions? RpcOptions);
}