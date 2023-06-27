namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.CancelExternalWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">Workflow run ID if any.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CancelExternalWorkflowInput(
        string ID,
        string? RunID);
}