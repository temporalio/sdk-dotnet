namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.PauseScheduleAsync" />.
    /// </summary>
    /// <param name="Id">Schedule ID.</param>
    /// <param name="Note">Pause note.</param>
    /// <param name="RpcOptions">RPC options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record PauseScheduleInput(
        string Id,
        string? Note,
        RpcOptions? RpcOptions);
}