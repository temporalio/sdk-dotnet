namespace Temporalio.Exceptions
{
    /// <summary>
    /// Occurs when the workflow has done something invalid.
    /// </summary>
    public class InvalidWorkflowOperationException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidWorkflowOperationException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        internal InvalidWorkflowOperationException(string message)
            : base(message)
        {
        }
    }
}