using System;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// A response that was received but has no usable secret value.
    /// </summary>
#pragma warning disable CA1064 // Internal, users only see it as an Exception
    internal sealed class InvalidResponseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Cause, if any.</param>
        public InvalidResponseException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
#pragma warning restore CA1064
}
