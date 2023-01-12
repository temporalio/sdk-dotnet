using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Base exception for all custom exceptions thrown by the Temporal library.
    /// </summary>
    public abstract class TemporalException : Exception
    {
        /// <inheritdoc />
        public TemporalException() { }

        /// <inheritdoc />
        public TemporalException(string message) : base(message) { }

        /// <inheritdoc />
        public TemporalException(string message, Exception? inner) : base(message, inner) { }
    }
}
