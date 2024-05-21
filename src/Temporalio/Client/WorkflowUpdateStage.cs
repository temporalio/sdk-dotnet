using Temporalio.Api.Enums.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Stage that an update can reach. This is used when starting an update to set the stage to
    /// wait for before returning.
    /// </summary>
    public enum WorkflowUpdateStage
    {
        /// <summary>
        /// Unset stage. This is an invalid value on start.
        /// </summary>
        None = UpdateWorkflowExecutionLifecycleStage.Unspecified,

        /// <summary>
        /// Admitted stage. This stage is reached when the server receives the update to process.
        /// This is currently an invalid value on start.
        /// </summary>
        Admitted = UpdateWorkflowExecutionLifecycleStage.Admitted,

        /// <summary>
        /// Accepted stage. This stage is reached when a workflow has received the update and either
        /// accepted (i.e. it has passed validation) or rejected it.
        /// </summary>
        Accepted = UpdateWorkflowExecutionLifecycleStage.Accepted,

        /// <summary>
        /// Completed stage. This stage is reached when a workflow has completed processing the
        /// update with either a success or failure.
        /// </summary>
        Completed = UpdateWorkflowExecutionLifecycleStage.Completed,
    }
}