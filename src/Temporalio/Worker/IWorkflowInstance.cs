using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCompletion;

namespace Temporalio.Worker
{
    /// <summary>
    /// Representation of a workflow instance that can be sent activations.
    /// </summary>
    internal interface IWorkflowInstance
    {
        /// <summary>
        /// Send activation and get completion.
        /// </summary>
        /// <param name="activation">Activation to apply to workflow.</param>
        /// <returns>Completion. This should have success or fail set.</returns>
        WorkflowActivationCompletion Activate(WorkflowActivation activation);
    }
}