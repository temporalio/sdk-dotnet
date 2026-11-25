using System;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// A fetch that took longer than the fetch timeout.
    /// </summary>
#pragma warning disable CA1064 // Internal, users only see it as an Exception
    internal sealed class FetchTimeoutException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FetchTimeoutException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Cause, if any.</param>
        public FetchTimeoutException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
#pragma warning restore CA1064
}
