using System;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Fixed handler names and error types of the workflow streams wire protocol. These are part
    /// of the cross-language contract and match the other SDKs' workflow streams packages exactly.
    /// The .NET SDK normally reserves the <c>__temporal_</c> prefix, but explicitly permits the
    /// <c>__temporal_workflow_stream_</c> sub-namespace for this package.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public static class WorkflowStreamConstants
    {
        /// <summary>
        /// Signal external publishers send to append a batch of items to the stream.
        /// </summary>
        public const string PublishSignalName = "__temporal_workflow_stream_publish";

        /// <summary>
        /// Update subscribers send to long-poll for new items.
        /// </summary>
        public const string PollUpdateName = "__temporal_workflow_stream_poll";

        /// <summary>
        /// Query that returns the current global offset.
        /// </summary>
        public const string OffsetQueryName = "__temporal_workflow_stream_offset";

        /// <summary>
        /// Application failure type returned by the poll update when the requested offset has
        /// already been truncated.
        /// </summary>
        public const string ErrorTypeTruncatedOffset = "TruncatedOffset";

        /// <summary>
        /// Application failure type thrown by <see cref="WorkflowStream.Truncate" /> when the
        /// requested offset is past the end of the log.
        /// </summary>
        public const string ErrorTypeTruncateOutOfRange = "TruncateOutOfRange";

        /// <summary>
        /// Application failure type the poll update's validator returns while the stream is
        /// detaching for continue-as-new. It tells a subscriber the rollover is in progress so it
        /// retries (rather than surfacing an error) until the poll lands on the successor run.
        /// </summary>
        public const string ErrorTypeStreamDraining = "StreamDraining";

        /// <summary>
        /// Default interval between automatic client-side publisher flushes.
        /// </summary>
        public static readonly TimeSpan DefaultBatchInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Default minimum interval between polls when no more items are immediately ready.
        /// </summary>
        public static readonly TimeSpan DefaultPollCooldown = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Default how long the workflow retains per-publisher dedup state since the publisher's
        /// last accepted batch.
        /// </summary>
        public static readonly TimeSpan DefaultPublisherTtl = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Default maximum time the client-side publisher retries a failed flush before surfacing
        /// a <see cref="FlushTimeoutException" />.
        /// </summary>
        public static readonly TimeSpan DefaultMaxRetryDuration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Caps the estimated wire size of a single poll response. Responses that would exceed
        /// this are truncated and signal <c>more_ready</c> so the subscriber pages through the
        /// remainder.
        /// </summary>
        internal const int MaxPollResponseBytes = 1_000_000;
    }
}
