namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.UpdateActivityOptionsAsync" />.
    /// </summary>
    /// <param name="Id">Activity ID.</param>
    /// <param name="RunId">Activity run ID if any.</param>
    /// <param name="Options">Options to update.</param>
    /// <param name="RpcOptions">RPC options for the call.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record UpdateActivityOptionsInput(
        string Id,
        string? RunId,
        ActivityOptionsUpdate Options,
        RpcOptions? RpcOptions);
}
