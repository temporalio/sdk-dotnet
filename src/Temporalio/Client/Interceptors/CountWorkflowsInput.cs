namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CountWorkflowsAsync" />.
    /// </summary>
    /// <param name="Query">Count query.</param>
    /// <param name="Options">Options passed in to count.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CountWorkflowsInput(
        string Query,
        WorkflowCountOptions? Options);
}