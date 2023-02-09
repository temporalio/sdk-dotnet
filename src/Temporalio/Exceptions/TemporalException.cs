using System;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Base exception for all custom exceptions thrown by the Temporal library.
    /// </summary>
    public abstract class TemporalException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalException"/> class.
        /// </summary>
        protected TemporalException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        protected TemporalException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="inner">Cause of the exception.</param>
        protected TemporalException(string message, Exception? inner)
            : base(message, inner)
        {
        }
    }
}
