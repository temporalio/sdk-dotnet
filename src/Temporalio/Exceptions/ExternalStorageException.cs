using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception for a failure storing payloads in, or retrieving them from, external storage. This
    /// is the base for every external storage failure.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal class ExternalStorageException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalStorageException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        public ExternalStorageException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalStorageException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="inner">Cause of the exception.</param>
        public ExternalStorageException(string message, Exception? inner)
            : base(message, inner)
        {
        }
    }
}
