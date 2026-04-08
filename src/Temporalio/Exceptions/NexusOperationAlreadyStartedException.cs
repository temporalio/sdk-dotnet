namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown by client when attempting to start a standalone Nexus operation that was
    /// already started.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationAlreadyStartedException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationAlreadyStartedException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="operationId">See <see cref="OperationId"/>.</param>
        /// <param name="runId">See <see cref="RunId"/>.</param>
        public NexusOperationAlreadyStartedException(
            string message, string operationId, string? runId)
            : base(message)
        {
            OperationId = operationId;
            RunId = runId;
        }

        /// <summary>
        /// Gets the operation ID that was already started.
        /// </summary>
        public string OperationId { get; private init; }

        /// <summary>
        /// Gets the run ID of the already-started operation, if available.
        /// </summary>
        public string? RunId { get; private init; }
    }
}
