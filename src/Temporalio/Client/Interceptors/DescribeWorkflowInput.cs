namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.DescribeWorkflowAsync" />.
    /// </summary>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">Workflow run ID if any.</param>
    /// <param name="Options">Options passed in to describe.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record DescribeWorkflowInput(
        string Id,
        string? RunId,
        WorkflowDescribeOptions? Options);
}