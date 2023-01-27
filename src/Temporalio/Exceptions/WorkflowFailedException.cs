using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a workflow has failed while waiting for the result.
    /// </summary>
    public class WorkflowFailedException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowFailedException"/> class.
        /// </summary>
        /// <param name="inner">Cause of the exception.</param>
        public WorkflowFailedException(Exception? inner)
            : base("Workflow failed", inner)
        {
        }
    }
}
