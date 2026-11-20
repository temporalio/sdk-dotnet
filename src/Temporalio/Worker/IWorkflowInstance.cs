using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    /// <summary>
    /// Representation of a workflow instance that can be sent activations.
    /// </summary>
    internal interface IWorkflowInstance : IWorkflowCodecHelperInstance
    {
        /// <summary>
        /// Send activation and get completion.
        /// </summary>
        /// <param name="activation">Activation to apply to workflow.</param>
        /// <returns>Completion. This should have success or fail set.</returns>
        WorkflowActivationCompletion Activate(WorkflowActivation activation);

        /// <summary>
        /// Gets the destination selected before a pending Nexus request was encoded,
        /// so storage routing does not have to reconstruct it from the envelope.
        /// </summary>
        /// <param name="seq">Operation sequence.</param>
        /// <returns>The destination, or null when no target was selected.</returns>
        IStorageDriverTargetInfo? GetPendingNexusOperationStorageTarget(uint seq);
    }
}