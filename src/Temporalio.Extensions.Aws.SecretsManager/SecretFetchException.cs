using System;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// A failed fetch of one or more secrets, with its failure_reason metric label.
    /// </summary>
#pragma warning disable CA1064 // Internal, users only see it as an Exception
    internal sealed class SecretFetchException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SecretFetchException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="reason">failure_reason metric label.</param>
        /// <param name="innerException">Cause, if any.</param>
        public SecretFetchException(string message, string reason, Exception? innerException = null)
            : base(message, innerException) => Reason = reason;

        /// <summary>
        /// Gets the failure_reason metric label.
        /// </summary>
        public string Reason { get; }
    }
#pragma warning restore CA1064
}
