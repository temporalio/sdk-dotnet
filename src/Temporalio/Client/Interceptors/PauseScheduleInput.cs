namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.PauseScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="Note">Pause note.</param>
    /// <param name="RpcOptions">RPC options.</param>
    public record PauseScheduleInput(
        string ID,
        string? Note,
        RpcOptions? RpcOptions);
}