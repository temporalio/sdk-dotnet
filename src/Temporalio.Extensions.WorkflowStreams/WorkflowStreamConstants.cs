using System;
using Temporalio.Runtime;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Fixed handler names and application-failure types in the Workflow Streams wire protocol.
    /// </summary>
    internal static class WorkflowStreamConstants
    {
        /// <summary>Signal used to append an external publisher's batch.</summary>
        internal const string PublishSignalName = TemporalRuntime.WorkflowStreamPublishSignalName;

        /// <summary>Update used to long-poll for stream items.</summary>
        internal const string PollUpdateName = TemporalRuntime.WorkflowStreamPollUpdateName;

        /// <summary>Query used to read the current global offset.</summary>
        internal const string OffsetQueryName = TemporalRuntime.WorkflowStreamOffsetQueryName;

        /// <summary>Failure type returned when a requested offset has been truncated.</summary>
        internal const string TruncatedOffsetErrorType = "TruncatedOffset";

        /// <summary>Failure type returned when truncation is requested past the log end.</summary>
        internal const string TruncateOutOfRangeErrorType = "TruncateOutOfRange";

        /// <summary>Failure type returned while pollers detach for continue-as-new.</summary>
        internal const string StreamDrainingErrorType = "StreamDraining";

        /// <summary>Keeps each update result comfortably below server payload limits.</summary>
        internal const int MaxPollResponseBytes = 1_000_000;

        /// <summary>Matches the batching cadence shared with other SDK implementations.</summary>
        internal static readonly TimeSpan DefaultBatchInterval = TimeSpan.FromSeconds(2);

        /// <summary>Stays below the default publisher TTL to preserve deduplication.</summary>
        internal static readonly TimeSpan DefaultMaxRetryDuration = TimeSpan.FromMinutes(10);

        /// <summary>Avoids tight polling while keeping interactive latency low.</summary>
        internal static readonly TimeSpan DefaultPollCooldown = TimeSpan.FromMilliseconds(100);

        /// <summary>Bounds deduplication metadata carried through continue-as-new.</summary>
        internal static readonly TimeSpan DefaultPublisherTtl = TimeSpan.FromMinutes(15);

        /// <summary>Bounds each transport attempt while retaining the accepted update handle.</summary>
        internal static readonly TimeSpan DefaultRpcTimeout = TimeSpan.FromSeconds(30);
    }
}
