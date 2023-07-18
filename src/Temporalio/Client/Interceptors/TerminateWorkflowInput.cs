namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.TerminateWorkflowAsync" />.
    /// </summary>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">Workflow run ID if any.</param>
    /// <param name="FirstExecutionRunId">Run that started the workflow chain to terminate.</param>
    /// <param name="Reason">Reason for termination.</param>
    /// <param name="Options">Options passed in to terminate.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record TerminateWorkflowInput(
        string Id,
        string? RunId,
        string? FirstExecutionRunId,
        string? Reason,
        WorkflowTerminateOptions? Options);
}