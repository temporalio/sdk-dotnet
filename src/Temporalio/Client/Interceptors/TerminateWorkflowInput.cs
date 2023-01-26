namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.TerminateWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">Workflow run ID if any.</param>
    /// <param name="FirstExecutionRunID">Run that started the workflow chain to cancel.</param>
    /// <param name="Reason">Reason for termination.</param>
    /// <param name="Options">Options passed in to cancel.</param>
    public record TerminateWorkflowInput(
        string ID,
        string? RunID,
        string? FirstExecutionRunID,
        string? Reason,
        WorkflowTerminateOptions? Options);
}