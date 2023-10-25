using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a workflow update has failed while waiting for the result.
    /// </summary>
    public class WorkflowUpdateFailedException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateFailedException"/> class.
        /// </summary>
        /// <param name="inner">Cause of the exception.</param>
        public WorkflowUpdateFailedException(Exception? inner)
            : base("Workflow update failed", inner)
        {
        }
    }
}
