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
        /// <inheritdoc />
        protected internal CancelledFailureException(
            string message,
            IReadOnlyCollection<object>? details = null
        ) : base(message)
        {
            Details = new OutboundFailureDetails(details ?? new object[0]);
        }

        /// <inheritdoc />
        protected internal CancelledFailureException(
            Failure failure,
            Exception? inner,
            Converters.IPayloadConverter converter
        ) : base(failure, inner)
        {
            var info =
                failure.CanceledFailureInfo
                ?? throw new ArgumentException("No cancelled failure info");
            Details = new InboundFailureDetails(converter, info.Details.Payloads_);
        }

        /// <inheritdoc />
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Access to the details of the exception. This is never null.
        /// </summary>
        /// <remarks>
        /// This will be <see cref="OutboundFailureDetails" /> for user-created exceptions and
        /// <see cref="InboundFailureDetails" /> for server-serialized exceptions.
        /// </remarks>
        public IFailureDetails Details { get; protected init; }
    }
}
