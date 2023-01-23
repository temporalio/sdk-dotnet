using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a workflow has failed while waiting for the result.
    /// </summary>
    public class WorkflowFailureException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowFailureException"/> class.
        /// </summary>
        /// <param name="inner">Cause of the exception.</param>
        public WorkflowFailureException(Exception? inner)
            : base("Workflow failed", inner)
        {
        }
    }
}
