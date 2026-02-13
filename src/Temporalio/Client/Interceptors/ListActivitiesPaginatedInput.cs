namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.ListActivitiesPaginatedAsync" />.
    /// </summary>
    /// <param name="Query">List query.</param>
    /// <param name="NextPageToken">Next page token from a previous response. Null if the request is for the first page.</param>
    /// <param name="Options">Options passed in to list.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ListActivitiesPaginatedInput(
        string Query,
        byte[]? NextPageToken,
        ActivityListPaginatedOptions? Options);
}
