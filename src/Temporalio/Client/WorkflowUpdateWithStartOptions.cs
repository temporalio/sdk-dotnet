namespace Temporalio.Client
{
    /// <summary>
    /// Options for executing an update with start.
    /// </summary>
    /// <remarks>NOTE: <see cref="StartWorkflowOperation"/> is required.</remarks>
    public class WorkflowUpdateWithStartOptions : WorkflowUpdateOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateWithStartOptions"/> class.
        /// </summary>
        /// <remarks>NOTE: <see cref="StartWorkflowOperation"/> is required.</remarks>
        public WorkflowUpdateWithStartOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateWithStartOptions"/> class.
        /// </summary>
        /// <param name="startWorkflowOperation">Workflow start operation.</param>
        public WorkflowUpdateWithStartOptions(WithStartWorkflowOperation startWorkflowOperation) =>
            StartWorkflowOperation = startWorkflowOperation;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateWithStartOptions"/> class.
        /// </summary>
        /// <param name="id">Update ID.</param>
        /// <param name="startWorkflowOperation">Workflow start operation.</param>
        public WorkflowUpdateWithStartOptions(string id, WithStartWorkflowOperation startWorkflowOperation)
            : base(id) => StartWorkflowOperation = startWorkflowOperation;

        /// <summary>
        /// Gets or sets the workflow start operation.
        /// </summary>
        /// <remarks>NOTE: This is required.</remarks>
        public WithStartWorkflowOperation? StartWorkflowOperation { get; set; }

        /// <summary>
        /// Create a shallow copy of these options including the start operation.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public override object Clone()
        {
            var copy = (WorkflowUpdateWithStartOptions)base.Clone();
            if (StartWorkflowOperation is { } startOperation)
            {
                copy.StartWorkflowOperation = (WithStartWorkflowOperation)startOperation.Clone();
            }
            return copy;
        }
    }
}