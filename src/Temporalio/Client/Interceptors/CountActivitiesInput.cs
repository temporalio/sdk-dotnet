namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CountActivitiesAsync" />.
    /// </summary>
    /// <param name="Query">Count query.</param>
    /// <param name="Options">Options passed in to count.</param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CountActivitiesInput(
        string Query,
        ActivityCountOptions? Options);
}
