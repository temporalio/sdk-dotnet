using System;
using Temporalio.Converters;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Options for constructing a <see cref="WorkflowStreamClient" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public class WorkflowStreamClientOptions
    {
        /// <summary>
        /// Gets or sets the interval between automatic flushes of the publisher's buffer.
        /// Default: 2 seconds.
        /// </summary>
        public TimeSpan BatchInterval { get; set; } = WorkflowStreamConstants.DefaultBatchInterval;

        /// <summary>
        /// Gets or sets the buffer size that triggers an automatic flush once reached. Zero (the
        /// default) disables size-based flushing.
        /// </summary>
        public int MaxBatchSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum time to retry a failed flush before surfacing a
        /// <see cref="FlushTimeoutException" />. Must be less than the workflow's publisher TTL
        /// (default 15 minutes) to preserve exactly-once delivery. Default: 10 minutes.
        /// </summary>
        public TimeSpan MaxRetryDuration { get; set; } = WorkflowStreamConstants.DefaultMaxRetryDuration;

        /// <summary>
        /// Gets or sets the converter that serializes published values into the per-item payloads
        /// carried inside each batch. If null, the Temporal client's payload converter is used.
        /// To decode subscribed items, use a converter compatible with this one.
        /// </summary>
        /// <remarks>
        /// Only payload conversion happens per item — never a payload codec (encryption,
        /// compression). The codec chain configured on the Temporal client runs once on the
        /// signal/update envelope that carries each batch, so encoding items here too would
        /// double-encode them; the <see cref="IPayloadConverter" /> type makes that mistake
        /// impossible.
        /// </remarks>
        public IPayloadConverter? PayloadConverter { get; set; }
    }
}
