using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// A serializable snapshot of stream state for continue-as-new. Thread a
    /// <see cref="WorkflowStreamState" /> through your workflow input and pass it to the
    /// <see cref="WorkflowStream" /> constructor on the next run.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Field names are part of the cross-language wire protocol; this type must serialize to JSON
    /// with exactly these names.
    /// </para>
    /// </remarks>
    public class WorkflowStreamState
    {
        /// <summary>
        /// Gets or sets the log entries. Per-item offsets are written as 0 in snapshots and
        /// re-derived as base offset + index on restore.
        /// </summary>
        [JsonPropertyName("log")]
#pragma warning disable CA1002, CA2227 // Mutable DTO for the wire protocol; List is intended
        public List<WireItem> Log { get; set; } = new();
#pragma warning restore CA1002, CA2227

        /// <summary>
        /// Gets or sets the global offset of the first log entry.
        /// </summary>
        [JsonPropertyName("base_offset")]
        public long BaseOffset { get; set; }

        /// <summary>
        /// Gets or sets the last accepted batch sequence per publisher ID, for dedup.
        /// </summary>
        [JsonPropertyName("publisher_sequences")]
#pragma warning disable CA2227 // Mutable DTO for the wire protocol
        public Dictionary<string, long> PublisherSequences { get; set; } = new();
#pragma warning restore CA2227

        /// <summary>
        /// Gets or sets the Unix seconds of the last accepted batch per publisher ID.
        /// </summary>
        [JsonPropertyName("publisher_last_seen")]
#pragma warning disable CA2227 // Mutable DTO for the wire protocol
        public Dictionary<string, double> PublisherLastSeen { get; set; } = new();
#pragma warning restore CA2227
    }
}
