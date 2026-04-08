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
        /// <param name="inner">Cause of the exception.</param>
        public NexusOperationFailedException(Exception? inner)
            : base("Nexus operation failed", inner)
        {
        }
    }
}
