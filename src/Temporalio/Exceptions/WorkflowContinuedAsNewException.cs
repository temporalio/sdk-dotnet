namespace Temporalio.Exceptions
{
    /// <summary>
    /// Thrown when a workflow continues as new and the caller is not following runs.
    /// </summary>
    public class WorkflowContinuedAsNewException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowContinuedAsNewException"/> class.
        /// </summary>
        /// <param name="newRunId">New run ID.</param>
        public WorkflowContinuedAsNewException(string newRunId)
            : base("Workflow continued as new") =>
            NewRunId = newRunId;

        /// <summary>
        /// Gets the run ID of the new run.
        /// </summary>
        public string NewRunId { get; private init; }
    }
}