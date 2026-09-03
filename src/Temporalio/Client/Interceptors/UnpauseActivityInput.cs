namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.UnpauseActivityAsync" />.
    /// </summary>
    /// <param name="Id">Activity ID.</param>
    /// <param name="RunId">Activity run ID if any.</param>
    /// <param name="Options">Options passed in to unpause.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record UnpauseActivityInput(
        string Id,
        string? RunId,
        ActivityUnpauseOptions? Options);
}
