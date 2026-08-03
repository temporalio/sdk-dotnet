using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// The poll update payload: a request to long-poll for new items.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Field names are part of the cross-language wire protocol; this type must serialize to JSON
    /// with exactly these names.
    /// </para>
    /// </remarks>
    public class PollInput
    {
        /// <summary>
        /// Gets or sets the topics to filter on. Empty means all topics.
        /// </summary>
        [JsonPropertyName("topics")]
#pragma warning disable CA1002, CA2227 // Mutable DTO for the wire protocol; List is intended
        public List<string> Topics { get; set; } = new();
#pragma warning restore CA1002, CA2227

        /// <summary>
        /// Gets or sets the global offset to start from. Zero means the beginning of whatever
        /// still exists.
        /// </summary>
        [JsonPropertyName("from_offset")]
        public long FromOffset { get; set; }
    }
}
