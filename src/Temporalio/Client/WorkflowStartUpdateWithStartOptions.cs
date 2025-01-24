namespace Temporalio.Client
{
    /// <summary>
    /// Options for executing an update with start.
    /// </summary>
    /// <remarks>NOTE: <see cref="WorkflowUpdateWithStartOptions.StartWorkflowOperation"/> and
    /// <see cref="WaitForStage"/> are both required.</remarks>
    public class WorkflowStartUpdateWithStartOptions : WorkflowUpdateWithStartOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStartUpdateWithStartOptions"/> class.
        /// </summary>
        /// <remarks>NOTE: <see cref="WorkflowUpdateWithStartOptions.StartWorkflowOperation"/> and
        /// <see cref="WaitForStage"/> are both required.</remarks>
        public WorkflowStartUpdateWithStartOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStartUpdateWithStartOptions"/> class.
        /// </summary>
        /// <param name="startWorkflowOperation">Workflow start operation.</param>
        /// <param name="waitForStage">Stage to wait for.</param>
        public WorkflowStartUpdateWithStartOptions(
            WithStartWorkflowOperation startWorkflowOperation, WorkflowUpdateStage waitForStage)
            : base(startWorkflowOperation) => WaitForStage = waitForStage;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStartUpdateWithStartOptions"/> class.
        /// </summary>
        /// <param name="id">Update ID.</param>
        /// <param name="startWorkflowOperation">Workflow start operation.</param>
        /// <param name="waitForStage">Stage to wait for.</param>
        public WorkflowStartUpdateWithStartOptions(
            string id, WithStartWorkflowOperation startWorkflowOperation, WorkflowUpdateStage waitForStage)
            : base(id, startWorkflowOperation) => WaitForStage = waitForStage;

        /// <summary>
        /// Gets or sets the stage to wait for on start. This is required and cannot be set to
        /// <c>None</c> or <c>Admitted</c> at this time.
        /// </summary>
        public WorkflowUpdateStage WaitForStage { get; set; }
    }
}