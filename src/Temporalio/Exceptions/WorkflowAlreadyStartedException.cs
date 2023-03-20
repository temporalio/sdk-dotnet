namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown by client when attempting to start a workflow that was already started.
    /// </summary>
    public class WorkflowAlreadyStartedException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowAlreadyStartedException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="workflowID">Already started workflow ID.</param>
        /// <param name="runID">Already started run ID.</param>
        public WorkflowAlreadyStartedException(string message, string workflowID, string runID)
            : base(message)
        {
            WorkflowID = workflowID;
            RunID = runID;
        }

        /// <summary>
        /// Gets the workflow ID that was already started.
        /// </summary>
        public string WorkflowID { get; private init; }

        /// <summary>
        /// Gets the run ID that was already started.
        /// </summary>
        public string RunID { get; private init; }
    }
}