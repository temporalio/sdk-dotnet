using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing timeout or cancellation on some client calls.
    /// </summary>
    public class RpcTimeoutOrCanceledException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RpcTimeoutOrCanceledException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="inner">Cause of the exception.</param>
        public RpcTimeoutOrCanceledException(string message, Exception? inner)
            : base(message, inner)
        {
        }
    }
}