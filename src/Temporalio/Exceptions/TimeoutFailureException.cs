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
        /// <inheritdoc />
        protected internal TimeoutFailureException(
            Failure failure,
            Exception? inner,
            Converters.IPayloadConverter converter
        ) : base(failure, inner)
        {
            var info =
                failure.TimeoutFailureInfo
                ?? throw new ArgumentException("No timeout failure info");
            LastHeartbeatDetails = new(converter, info.LastHeartbeatDetails.Payloads_);
        }

        /// <inheritdoc />
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Type of timeout that occurred.
        /// </summary>
        public TimeoutType TimeoutType => Failure!.TimeoutFailureInfo.TimeoutType;

        /// <summary>
        /// Last heartbeat details of the activity if applicable.
        /// </summary>
        public InboundFailureDetails LastHeartbeatDetails { get; protected init; }
    }
}
