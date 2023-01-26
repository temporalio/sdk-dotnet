namespace Temporalio.Exceptions
{
    /// <summary>
    /// Thrown when a query fails on the worker.
    /// </summary>
    public class WorkflowQueryFailedException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowQueryFailedException"/> class.
        /// </summary>
        /// <param name="message">Reason for failure.</param>
        public WorkflowQueryFailedException(string message)
            : base(message)
        {
        }
    }
}