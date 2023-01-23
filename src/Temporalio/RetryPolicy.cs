using System;
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;

namespace Temporalio {
    public record RetryPolicy {
        public TimeSpan InitialInterval { get; set; } = TimeSpan.FromSeconds(1);
        public float BackoffCoefficient { get; set; } = 2.0F;
        public TimeSpan? MaximumInterval { get; set; }
        public int MaximumAttempts { get; set; }
        public IReadOnlyCollection<string>? NonRetryableErrorTypes { get; set; }

        public Api.Common.V1.RetryPolicy ToProto()
        {
            var proto = new Api.Common.V1.RetryPolicy()
            {
                InitialInterval = Duration.FromTimeSpan(InitialInterval),
                BackoffCoefficient = BackoffCoefficient,
                MaximumInterval = MaximumInterval == null ? null : Duration.FromTimeSpan((TimeSpan)MaximumInterval),
                MaximumAttempts = MaximumAttempts,
            };
            if (NonRetryableErrorTypes != null && NonRetryableErrorTypes.Count > 0) {
                proto.NonRetryableErrorTypes.AddRange(NonRetryableErrorTypes);
            }
            return proto;
        }
    }
}