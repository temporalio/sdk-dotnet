using System;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.PollWorkflowUpdateAsync" />.
    /// </summary>
    /// <param name="Id">Update ID.</param>
    /// <param name="WorkflowId">Workflow ID.</param>
    /// <param name="WorkflowRunId">Workflow run ID if any.</param>
    /// <param name="Timeout">Timeout.</param>
    /// <param name="RpcOptions">RPC Options if any.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record PollWorkflowUpdateInput(
        string Id,
        string WorkflowId,
        string? WorkflowRunId,
        TimeSpan Timeout,
        RpcOptions? RpcOptions);
}