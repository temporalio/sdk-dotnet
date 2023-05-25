using System;
using System.Collections.Generic;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a cancellation.
    /// </summary>
    public class CancelledFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CancelledFailureException"/> class.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="details">Details for the exception.</param>
        internal protected CancelledFailureException(
            string message,
            IReadOnlyCollection<object?>? details = null)
            : base(message) =>
            Details = new OutboundFailureDetails(details ?? Array.Empty<object?>());

        /// <summary>
        /// Initializes a new instance of the <see cref="CancelledFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        /// <param name="converter">Converter used for converting details.</param>
        internal protected CancelledFailureException(
            Failure failure,
            Exception? inner,
            Converters.IPayloadConverter converter)
            : base(failure, inner)
        {
            var info =
                failure.CanceledFailureInfo
                ?? throw new ArgumentException("No cancelled failure info");
            Details = new InboundFailureDetails(converter, info.Details?.Payloads_);
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets the details of the exception. This is never null.
        /// </summary>
        /// <remarks>
        /// This will be <see cref="OutboundFailureDetails" /> for user-created exceptions and
        /// <see cref="InboundFailureDetails" /> for server-serialized exceptions.
        /// </remarks>
        public IFailureDetails Details { get; protected init; }
    }
}
