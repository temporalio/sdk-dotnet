namespace Temporalio.Exceptions
{
    /// <summary>
    /// Occurs when the workflow does something outside of the workflow scheduler.
    /// </summary>
    public class InvalidWorkflowSchedulerException : InvalidWorkflowOperationException
    {
        private readonly string? stackTraceOverride;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidWorkflowSchedulerException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="stackTraceOverride">Override of stack trace.</param>
        internal InvalidWorkflowSchedulerException(string message, string? stackTraceOverride = null)
            : base(message) =>
            this.stackTraceOverride = stackTraceOverride;

        /// <inheritdoc />
        public override string? StackTrace => stackTraceOverride ?? base.StackTrace;
    }
}