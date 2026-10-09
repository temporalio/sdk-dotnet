namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a child workflow cannot start because its versioning override is
    /// invalid.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change in the future.</remarks>
    public class InvalidVersioningOverrideException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidVersioningOverrideException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="workflowId">See <see cref="WorkflowId"/>.</param>
        /// <param name="workflowType">See <see cref="WorkflowType"/>.</param>
        internal InvalidVersioningOverrideException(
            string message, string workflowId, string workflowType)
            : base(message)
        {
            WorkflowId = workflowId;
            WorkflowType = workflowType;
        }

        /// <summary>
        /// Gets the workflow ID that was attempted to start.
        /// </summary>
        public string WorkflowId { get; private init; }

        /// <summary>
        /// Gets the workflow type that was attempted to start.
        /// </summary>
        public string WorkflowType { get; private init; }
    }
}
