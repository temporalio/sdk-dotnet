using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a payload is marked as a reference to externally stored data but its
    /// contents cannot be read as one.
    /// </summary>
    /// <remarks>
    /// This wraps the underlying parse failure, which can be one of two unrelated protobuf exception
    /// types depending on whether the data is malformed JSON or valid JSON of the wrong shape.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class InvalidExternalStorageReferenceException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="InvalidExternalStorageReferenceException"/> class.
        /// </summary>
        /// <param name="inner">Cause of the exception.</param>
        public InvalidExternalStorageReferenceException(Exception? inner)
            : base(
                "Payload is marked as an external storage reference but could not be read as one.",
                inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="InvalidExternalStorageReferenceException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="inner">Cause of the exception.</param>
        public InvalidExternalStorageReferenceException(string message, Exception? inner)
            : base(message, inner)
        {
        }
    }
}
