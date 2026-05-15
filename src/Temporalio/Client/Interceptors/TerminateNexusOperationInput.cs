namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.TerminateNexusOperationAsync" />.
    /// </summary>
    /// <param name="Id">Operation ID.</param>
    /// <param name="RunId">Operation run ID if any.</param>
    /// <param name="Options">Options passed in to terminate.</param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record TerminateNexusOperationInput(
        string Id,
        string? RunId,
        NexusOperationTerminateOptions? Options);
}
