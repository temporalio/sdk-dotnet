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
        /// <param name="workflowId">Already started workflow ID.</param>
        /// <param name="runId">Already started run ID.</param>
        public WorkflowAlreadyStartedException(string message, string workflowId, string runId)
            : base(message)
        {
            WorkflowId = workflowId;
            RunId = runId;
        }

        /// <summary>
        /// Gets the workflow ID that was already started.
        /// </summary>
        public string WorkflowId { get; private init; }

        /// <summary>
        /// Gets the run ID that was already started.
        /// </summary>
        public string RunId { get; private init; }
    }
}