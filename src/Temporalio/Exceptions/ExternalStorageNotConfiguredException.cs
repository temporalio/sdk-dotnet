namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a payload stored in external storage is encountered but no external
    /// storage is configured to retrieve it.
    /// </summary>
    /// <remarks>
    /// This usually means the client or worker reading the payload is configured differently from
    /// the one that wrote it. Any component that reads a payload must have the driver that stored it
    /// configured.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class ExternalStorageNotConfiguredException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ExternalStorageNotConfiguredException"/> class.
        /// </summary>
        public ExternalStorageNotConfiguredException()
            : base("[TMPRL1105] Encountered an externally stored payload but external storage is " +
                "not configured.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ExternalStorageNotConfiguredException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        public ExternalStorageNotConfiguredException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ExternalStorageNotConfiguredException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="inner">Cause of the exception.</param>
        public ExternalStorageNotConfiguredException(string message, System.Exception? inner)
            : base(message, inner)
        {
        }
    }
}
