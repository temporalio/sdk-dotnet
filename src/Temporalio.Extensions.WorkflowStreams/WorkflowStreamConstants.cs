namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Fixed handler names and application-failure types in the Workflow Streams wire protocol.
    /// </summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public static class WorkflowStreamConstants
    {
        /// <summary>Signal used to append an external publisher's batch.</summary>
        public const string PublishSignalName = "__temporal_workflow_stream_publish";

        /// <summary>Update used to long-poll for stream items.</summary>
        public const string PollUpdateName = "__temporal_workflow_stream_poll";

        /// <summary>Query used to read the current global offset.</summary>
        public const string OffsetQueryName = "__temporal_workflow_stream_offset";

        /// <summary>Failure type returned when a requested offset has been truncated.</summary>
        public const string TruncatedOffsetErrorType = "TruncatedOffset";

        /// <summary>Failure type returned when truncation is requested past the end of the log.</summary>
        public const string TruncateOutOfRangeErrorType = "TruncateOutOfRange";

        /// <summary>Failure type returned while pollers are detaching for continue-as-new.</summary>
        public const string StreamDrainingErrorType = "StreamDraining";

        /// <summary>Keeps each update result comfortably below server payload limits.</summary>
        internal const int MaxPollResponseBytes = 1_000_000;

        /// <summary>Identifies items that cannot be delivered by the paging protocol.</summary>
        internal const string OversizedItemErrorType = "WorkflowStreamItemTooLarge";

        /// <summary>Prevents corrupt retained state from retrying its workflow task forever.</summary>
        internal const string InvalidStateErrorType = "WorkflowStreamInvalidState";

        /// <summary>Matches the batching cadence shared with other SDK implementations.</summary>
        internal static readonly System.TimeSpan DefaultBatchInterval = System.TimeSpan.FromSeconds(2);

        /// <summary>Stays below the default publisher TTL to preserve deduplication.</summary>
        internal static readonly System.TimeSpan DefaultMaxRetryDuration = System.TimeSpan.FromMinutes(10);

        /// <summary>Avoids tight polling while keeping interactive latency low.</summary>
        internal static readonly System.TimeSpan DefaultPollCooldown = System.TimeSpan.FromMilliseconds(100);

        /// <summary>Bounds deduplication metadata carried through continue-as-new.</summary>
        internal static readonly System.TimeSpan DefaultPublisherTtl = System.TimeSpan.FromMinutes(15);

        /// <summary>Bounds each transport attempt while retaining the accepted update handle.</summary>
        internal static readonly System.TimeSpan DefaultRpcTimeout = System.TimeSpan.FromSeconds(30);
    }
}
