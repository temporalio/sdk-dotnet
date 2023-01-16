using System;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Base exception for all Temporal-failure-based exceptions.
    /// </summary>
    /// <remarks>
    /// All exceptions of this type fail a workflow.
    /// </remarks>
    public class FailureException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FailureException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="inner">Cause of the exception.</param>
        internal protected FailureException(string message, Exception? inner = null)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        internal protected FailureException(Failure failure, Exception? inner)
            : base(failure.Message, inner)
        {
            Failure = failure;
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        /// <remarks>
        /// This is non-null except for user-created exceptions.
        /// </remarks>
        public Failure? Failure { get; protected init; } = null;

        /// <summary>
        /// Gets the stack trace on the exception or on the failure if not on the exception.
        /// </summary>
        public override string? StackTrace
        {
            get
            {
                var stackTrace = base.StackTrace;
                if (
                    string.IsNullOrEmpty(stackTrace)
                    && Failure != null
                    && Failure.StackTrace.Length > 0)
                {
                    stackTrace = Failure.StackTrace;
                }
                return stackTrace;
            }
        }
    }
}
