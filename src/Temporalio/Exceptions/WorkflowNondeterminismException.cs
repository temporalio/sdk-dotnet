namespace Temporalio.Exceptions
{
    /// <summary>
    /// Occurs when the workflow does something non-deterministic.
    /// </summary>
    /// <remarks>
    /// This is usually only thrown in a replayer and therefore can't be caught in a workflow, but
    /// this exception can be used as a failure exception type to have non-deterministic exceptions
    /// fail workflows.
    /// </remarks>
    public class WorkflowNondeterminismException : InvalidWorkflowOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowNondeterminismException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        internal WorkflowNondeterminismException(string message)
            : base(message)
        {
        }
    }
}