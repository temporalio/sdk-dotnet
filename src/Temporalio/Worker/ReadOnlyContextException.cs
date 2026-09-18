using System;

namespace Temporalio.Worker
{
    /// <summary>
    /// Exception thrown when a read-only workflow context (a query, an update validator, a wait
    /// condition callback, or a patch activation callback) attempts an operation that is not
    /// allowed there.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="InvalidOperationException"/> so it stays catchable as before. It
    /// exists so the update path can tell a worker bug apart from a business rejection.
    /// </remarks>
    internal class ReadOnlyContextException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyContextException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public ReadOnlyContextException(string message)
            : base(message)
        {
        }
    }
}
