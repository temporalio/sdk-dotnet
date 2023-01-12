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
        /// Get the underlying protobuf failure object.
        /// </summary>
        /// <remarks>
        /// This is non-null except for user-created exceptions.
        /// </remarks>
        public Failure? Failure { get; protected init; } = null;

        /// <summary>
        /// Create a user-created failure exception with a message.
        /// </summary>
        protected internal FailureException(string message, Exception? inner = null)
            : base(message, inner) { }

        /// <summary>
        /// Create a server-created failure exception with a failure object.
        /// </summary>
        protected internal FailureException(Failure failure, Exception? inner)
            : base(failure.Message, inner)
        {
            Failure = failure;
        }

        /// <summary>
        /// Stack trace on the exception or on the failure if not on the exception.
        /// </summary>
        public override string? StackTrace
        {
            get
            {
                var stackTrace = base.StackTrace;
                if (
                    String.IsNullOrEmpty(stackTrace)
                    && Failure != null
                    && Failure.StackTrace.Length > 0
                )
                {
                    stackTrace = Failure.StackTrace;
                }
                return stackTrace;
            }
        }
    }
}
