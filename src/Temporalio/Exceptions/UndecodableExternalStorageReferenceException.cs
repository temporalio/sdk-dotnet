using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a payload marked as an external storage reference does not decode to
    /// one.
    /// </summary>
    /// <remarks>
    /// This usually means the payload codec that wrote the reference is not the one configured to
    /// read it.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class UndecodableExternalStorageReferenceException : ExternalStorageException
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="UndecodableExternalStorageReferenceException"/> class.
        /// </summary>
        public UndecodableExternalStorageReferenceException()
            : base("[TMPRL1106] Encountered an encoded external storage reference that could not " +
                "be decoded. Check that the payload codec used to write it is configured.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="UndecodableExternalStorageReferenceException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        public UndecodableExternalStorageReferenceException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="UndecodableExternalStorageReferenceException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="inner">Cause of the exception.</param>
        public UndecodableExternalStorageReferenceException(string message, Exception? inner)
            : base(message, inner)
        {
        }
    }
}
