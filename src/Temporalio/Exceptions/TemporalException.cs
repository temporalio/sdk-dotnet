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

        /// <summary>
        /// Whether the given exception is a .NET cancellation or a Temporal cancellation (including
        /// a cancellation inside an activity or child exception).
        /// </summary>
        /// <param name="e">Exception to check.</param>
        /// <returns>True if the exception is due to a cancellation.</returns>
        /// <remarks>
        /// This is useful to determine whether a client/in-workflow caught exception is due to
        /// cancellation. Temporal wraps exceptions or reuses .NET cancellation exceptions, so a
        /// simple type check is not enough to be sure.
        /// </remarks>
        public static bool IsCancelledException(Exception e) =>
            // It is important that the .NET cancelled exception is included because it is natural
            // for users to see that as a cancelled exception and it is sometimes thrown from
            // workflow situations like cancelled timers.
            e is OperationCanceledException ||
            e is CancelledFailureException ||
            (e as ActivityFailureException)?.InnerException is CancelledFailureException ||
            (e as ChildWorkflowFailureException)?.InnerException is CancelledFailureException;
    }
}
