namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.TerminateWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">Workflow run ID if any.</param>
    /// <param name="FirstExecutionRunID">Run that started the workflow chain to terminate.</param>
    /// <param name="Reason">Reason for termination.</param>
    /// <param name="Options">Options passed in to terminate.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record TerminateWorkflowInput(
        string ID,
        string? RunID,
        string? FirstExecutionRunID,
        string? Reason,
        WorkflowTerminateOptions? Options);
}