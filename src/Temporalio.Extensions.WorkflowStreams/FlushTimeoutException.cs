using System;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Thrown when a pending batch's flush retries exceeded the client's max retry duration. The
    /// pending batch is dropped; the items may or may not have been delivered, since the
    /// exactly-once window expired.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public class FlushTimeoutException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlushTimeoutException"/> class.
        /// </summary>
        /// <param name="message">Required message for the exception.</param>
        public FlushTimeoutException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FlushTimeoutException"/> class.
        /// </summary>
        /// <param name="message">Required message for the exception.</param>
        /// <param name="inner">Cause of the exception.</param>
        public FlushTimeoutException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
