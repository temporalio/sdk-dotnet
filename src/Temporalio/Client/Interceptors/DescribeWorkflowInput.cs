namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.DescribeWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">Workflow run ID if any.</param>
    /// <param name="Options">Options passed in to describe.</param>
    public record DescribeWorkflowInput(
        string ID,
        string? RunID,
        WorkflowDescribeOptions? Options);
}