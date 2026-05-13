using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a standalone Nexus operation has failed while waiting for the result.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationFailedException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationFailedException"/> class.
        /// </summary>
        /// <param name="operationId">Operation ID.</param>
        /// <param name="runId">Operation run ID.</param>
        /// <param name="inner">Cause of the exception.</param>
        public NexusOperationFailedException(string operationId, string? runId, Exception? inner)
            : base("Nexus operation failed", inner)
        {
            OperationId = operationId;
            RunId = runId;
        }

        /// <summary>
        /// Gets the operation ID.
        /// </summary>
        public string OperationId { get; }

        /// <summary>
        /// Gets the operation run ID, if known.
        /// </summary>
        public string? RunId { get; }
    }
}
