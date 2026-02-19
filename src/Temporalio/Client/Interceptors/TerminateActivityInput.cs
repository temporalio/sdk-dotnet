namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.TerminateActivityAsync" />.
    /// </summary>
    /// <param name="Id">Activity ID.</param>
    /// <param name="RunId">Activity run ID if any.</param>
    /// <param name="Options">Options passed in to terminate.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record TerminateActivityInput(
        string Id,
        string? RunId,
        ActivityTerminateOptions? Options);
}
