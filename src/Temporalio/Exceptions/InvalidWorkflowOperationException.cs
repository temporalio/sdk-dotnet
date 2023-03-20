namespace Temporalio.Exceptions
{
    /// <summary>
    /// Occurs when the workflow has done something invalid.
    /// </summary>
    public class InvalidWorkflowOperationException : TemporalException
    {
        private readonly string? stackTraceOverride;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidWorkflowOperationException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="stackTraceOverride">Override of stack trace.</param>
        internal InvalidWorkflowOperationException(string message, string? stackTraceOverride)
            : base(message) =>
            this.stackTraceOverride = stackTraceOverride;

        /// <inheritdoc />
        public override string? StackTrace => stackTraceOverride ?? base.StackTrace;
    }
}