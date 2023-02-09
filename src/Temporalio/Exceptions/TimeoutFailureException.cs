using System;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a timeout.
    /// </summary>
    public class TimeoutFailureException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeoutFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        /// <param name="converter">Converter used for converting details.</param>
        internal protected TimeoutFailureException(
            Failure failure,
            Exception? inner,
            Converters.IPayloadConverter converter)
            : base(failure, inner)
        {
            var info =
                failure.TimeoutFailureInfo
                ?? throw new ArgumentException("No timeout failure info");
            LastHeartbeatDetails = new(converter, info.LastHeartbeatDetails?.Payloads_);
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets the type of timeout that occurred.
        /// </summary>
        public TimeoutType TimeoutType => Failure!.TimeoutFailureInfo.TimeoutType;

        /// <summary>
        /// Gets the last heartbeat details of the activity if applicable.
        /// </summary>
        public InboundFailureDetails LastHeartbeatDetails { get; protected init; }
    }
}
