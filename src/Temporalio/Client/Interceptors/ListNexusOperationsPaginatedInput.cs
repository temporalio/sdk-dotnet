namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.ListNexusOperationsPaginatedAsync" />.
    /// </summary>
    /// <param name="Query">List query.</param>
    /// <param name="NextPageToken">Next page token from a previous call, or null.</param>
    /// <param name="Options">Options passed in to list.</param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ListNexusOperationsPaginatedInput(
        string Query,
        byte[]? NextPageToken,
        NexusOperationListPaginatedOptions? Options);
}
