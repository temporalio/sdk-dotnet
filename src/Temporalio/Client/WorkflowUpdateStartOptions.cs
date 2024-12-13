namespace Temporalio.Client
{
    /// <summary>
    /// Options for starting an update on a <see cref="WorkflowHandle" />.
    /// </summary>
    public class WorkflowUpdateStartOptions : WorkflowUpdateOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateStartOptions"/> class.
        /// </summary>
        public WorkflowUpdateStartOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateStartOptions"/> class.
        /// </summary>
        /// <param name="waitForStage">Stage to wait for.</param>
        public WorkflowUpdateStartOptions(WorkflowUpdateStage waitForStage) =>
            WaitForStage = waitForStage;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateStartOptions"/> class.
        /// </summary>
        /// <param name="id">Update ID.</param>
        /// <param name="waitForStage">Stage to wait for.</param>
        public WorkflowUpdateStartOptions(string id, WorkflowUpdateStage waitForStage)
            : base(id) => WaitForStage = waitForStage;

        /// <summary>
        /// Gets or sets the stage to wait for on start. This is required and cannot be set to
        /// <c>None</c> or <c>Admitted</c> at this time.
        /// </summary>
        public WorkflowUpdateStage WaitForStage { get; set; }
    }
}