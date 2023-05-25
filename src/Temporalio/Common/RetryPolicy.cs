using System;
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;

namespace Temporalio.Common
{
    /// <summary>
    /// Retry policy for workflows and activities.
    /// </summary>
    public record RetryPolicy
    {
        /// <summary>
        /// Gets or sets the backoff interval for the first retry. Default is 1s.
        /// </summary>
        public TimeSpan InitialInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the coefficient to multiply previous backoff interval by to get a new
        /// interval on each retry. Default is 2.0.
        /// </summary>
        public float BackoffCoefficient { get; set; } = 2.0F;

        /// <summary>
        /// Gets or sets the maximum backoff interval between retries.
        /// </summary>
        public TimeSpan? MaximumInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of attempts. If 0, the default, there is no maximum.
        /// </summary>
        public int MaximumAttempts { get; set; }

        /// <summary>
        /// Gets or sets the collection of error types that are not retryable.
        /// </summary>
        public IReadOnlyCollection<string>? NonRetryableErrorTypes { get; set; }

        /// <summary>
        /// Convert this retry policy to its protobuf equivalent.
        /// </summary>
        /// <returns>Protobuf retry policy.</returns>
        internal Api.Common.V1.RetryPolicy ToProto()
        {
            var proto = new Api.Common.V1.RetryPolicy()
            {
                InitialInterval = Duration.FromTimeSpan(InitialInterval),
                BackoffCoefficient = BackoffCoefficient,
                MaximumInterval = MaximumInterval == null ? null : Duration.FromTimeSpan((TimeSpan)MaximumInterval),
                MaximumAttempts = MaximumAttempts,
            };
            if (NonRetryableErrorTypes != null && NonRetryableErrorTypes.Count > 0)
            {
                proto.NonRetryableErrorTypes.AddRange(NonRetryableErrorTypes);
            }
            return proto;
        }

        /// <summary>
        /// Convert a protobuf retry policy to this type.
        /// </summary>
        /// <param name="proto">Protobuf retry policy.</param>
        /// <returns>Retry policy.</returns>
        internal static RetryPolicy FromProto(Api.Common.V1.RetryPolicy proto)
        {
            return new()
            {
                InitialInterval = proto.InitialInterval.ToTimeSpan(),
                BackoffCoefficient = (float)proto.BackoffCoefficient,
                MaximumInterval = proto.MaximumInterval?.ToTimeSpan(),
                MaximumAttempts = proto.MaximumAttempts,
                NonRetryableErrorTypes = proto.NonRetryableErrorTypes.Count == 0 ? null : proto.NonRetryableErrorTypes,
            };
        }
    }
}