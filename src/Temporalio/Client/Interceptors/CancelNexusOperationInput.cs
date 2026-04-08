namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CancelNexusOperationAsync" />.
    /// </summary>
    /// <param name="Id">Operation ID.</param>
    /// <param name="RunId">Operation run ID if any.</param>
    /// <param name="Options">Options passed in to cancel.</param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CancelNexusOperationInput(
        string Id,
        string? RunId,
        NexusOperationCancelOptions? Options);
}
