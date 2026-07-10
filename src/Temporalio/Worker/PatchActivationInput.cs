using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Input for <see cref="TemporalWorkerOptions.PatchActivationCallback" />.
    /// </summary>
    /// <param name="WorkflowInfo">Information about the workflow execution calling
    /// <see cref="Workflow.Patched" />.</param>
    /// <param name="PatchId">Patch ID passed to <see cref="Workflow.Patched" />.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record PatchActivationInput(
        WorkflowInfo WorkflowInfo,
        string PatchId);
}
