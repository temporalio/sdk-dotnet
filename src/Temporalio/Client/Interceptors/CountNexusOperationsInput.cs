namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CountNexusOperationsAsync" />.
    /// </summary>
    /// <param name="Query">Count query.</param>
    /// <param name="Options">Options passed in to count.</param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CountNexusOperationsInput(
        string Query,
        NexusOperationCountOptions? Options);
}
