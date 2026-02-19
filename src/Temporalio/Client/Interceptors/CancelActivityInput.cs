namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CancelActivityAsync" />.
    /// </summary>
    /// <param name="Id">Activity ID.</param>
    /// <param name="RunId">Activity run ID if any.</param>
    /// <param name="Options">Options passed in to cancel.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CancelActivityInput(
        string Id,
        string? RunId,
        ActivityCancelOptions? Options);
}
