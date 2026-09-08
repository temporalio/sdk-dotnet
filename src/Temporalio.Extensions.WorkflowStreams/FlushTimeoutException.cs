using System;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Exception thrown when an ambiguously delivered publish batch exceeds its retry duration.
    /// </summary>
    /// <remarks>
    /// The batch is dropped locally. It is present in the workflow log if the signal reached the
    /// server, and otherwise is lost. WARNING: Workflow Streams is experimental and may change.
    /// </remarks>
    public class FlushTimeoutException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="FlushTimeoutException"/> class.</summary>
        /// <param name="message">Description of the expired ambiguous-delivery window.</param>
        internal FlushTimeoutException(string message)
            : base(message)
        {
        }
    }
}
