using System;
using System.Collections.Generic;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing an error in user code.
    /// </summary>
    /// <remarks>
    /// For workflows, users should throw this exception to signal a workflow failure. Other
    /// non-Temporal exceptions will not fail the workflow.
    /// <para>
    /// In activities, all non-Temporal exceptions are translated to this exception as retryable
    /// with the <see cref="Type" /> as the unqualified exception class name.
    /// </para>
    /// </remarks>
    public class ApplicationFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationFailureException"/> class.
        /// </summary>
        /// <param name="message">Required message for the exception.</param>
        /// <param name="errorType">Optional string type name of the exception.</param>
        /// <param name="nonRetryable">If true, marks the exception as non-retryable.</param>
        /// <param name="details">Collection of details to serialize into the exception.</param>
        /// <param name="nextRetryDelay">Override the next retry delay with this value.</param>
        public ApplicationFailureException(
            string message,
            string? errorType = null,
            bool nonRetryable = false,
            IReadOnlyCollection<object?>? details = null,
            TimeSpan? nextRetryDelay = null)
            : base(message)
        {
            ErrorType = errorType;
            NonRetryable = nonRetryable;
            Details = new OutboundFailureDetails(details ?? Array.Empty<object?>());
            NextRetryDelay = nextRetryDelay;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationFailureException"/> class.
        /// </summary>
        /// <param name="message">Required message for the exception.</param>
        /// <param name="inner">
        /// Cause of the exception (can use other constructor if no cause).
        /// </param>
        /// <param name="errorType">Optional string type name of the exception.</param>
        /// <param name="nonRetryable">If true, marks the exception as non-retryable.</param>
        /// <param name="details">Collection of details to serialize into the exception.</param>
        /// <param name="nextRetryDelay">Override the next retry delay with this value.</param>
        public ApplicationFailureException(
            string message,
            Exception? inner,
            string? errorType = null,
            bool nonRetryable = false,
            IReadOnlyCollection<object?>? details = null,
            TimeSpan? nextRetryDelay = null)
            : base(message, inner)
        {
            ErrorType = errorType;
            NonRetryable = nonRetryable;
            Details = new OutboundFailureDetails(details);
            NextRetryDelay = nextRetryDelay;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        /// <param name="converter">Converter used for converting details.</param>
        internal protected ApplicationFailureException(
            Failure failure,
            Exception? inner,
            Converters.IPayloadConverter converter)
            : base(failure, inner)
        {
            var info =
                failure.ApplicationFailureInfo
                ?? throw new ArgumentException("No application failure info");
            ErrorType = info.Type.Length == 0 ? null : info.Type;
            NonRetryable = info.NonRetryable;
            Details = new InboundFailureDetails(converter, info.Details?.Payloads_);
            NextRetryDelay = info.NextRetryDelay?.ToTimeSpan();
        }

        /// <summary>
        /// Gets the string "type" of the exception if any.
        /// </summary>
        public string? ErrorType { get; protected init; }

        /// <summary>
        /// Gets a value indicating whether this exception is non-retryable.
        /// </summary>
        public bool NonRetryable { get; protected init; }

        /// <summary>
        /// Gets the details of the exception. This is never null.
        /// </summary>
        /// <remarks>
        /// This will be <see cref="OutboundFailureDetails" /> for user-created exceptions and
        /// <see cref="InboundFailureDetails" /> for server-serialized exceptions.
        /// </remarks>
        public IFailureDetails Details { get; protected init; }

        /// <summary>
        /// Gets the next retry delay override if any was set.
        /// </summary>
        public TimeSpan? NextRetryDelay { get; protected init; }
    }
}
