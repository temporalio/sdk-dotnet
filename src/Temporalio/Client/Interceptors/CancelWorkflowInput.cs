namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CancelWorkflowAsync" />.
    /// </summary>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">Workflow run ID if any.</param>
    /// <param name="FirstExecutionRunId">Run that started the workflow chain to cancel.</param>
    /// <param name="Options">Options passed in to cancel.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CancelWorkflowInput(
        string Id,
        string? RunId,
        string? FirstExecutionRunId,
        WorkflowCancelOptions? Options);
}