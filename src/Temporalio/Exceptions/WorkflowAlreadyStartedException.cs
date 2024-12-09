using System;

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
        /// <param name="workflowId">See <see cref="WorkflowId"/>.</param>
        /// <param name="runId">See <see cref="RunId"/>.</param>
        [Obsolete("Use other constructor")]
        public WorkflowAlreadyStartedException(string message, string workflowId, string runId)
            : this(message, workflowId, "<unknown>", runId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowAlreadyStartedException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="workflowId">See <see cref="WorkflowId"/>.</param>
        /// <param name="workflowType">See <see cref="WorkflowType"/>.</param>
        /// <param name="runId">See <see cref="RunId"/>.</param>
        public WorkflowAlreadyStartedException(
            string message, string workflowId, string workflowType, string runId)
            : base(message)
        {
            WorkflowId = workflowId;
            WorkflowType = workflowType;
            RunId = runId;
        }

        /// <summary>
        /// Gets the workflow ID that was already started.
        /// </summary>
        public string WorkflowId { get; private init; }

        /// <summary>
        /// Gets the workflow type that was attempted to start.
        /// </summary>
        public string WorkflowType { get; private init; }

        /// <summary>
        /// Gets the run ID that was already started. This may be <c>&lt;unknown&gt;</c> when this
        /// error is thrown for a child workflow from inside a workflow.
        /// </summary>
        public string RunId { get; private init; }
    }
}