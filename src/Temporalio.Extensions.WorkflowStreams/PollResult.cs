using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// The poll update response: items matching the poll request. When <see cref="MoreReady" /> is
    /// true the response was truncated to stay within size limits and the subscriber should poll
    /// again immediately rather than applying a cooldown.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Field names are part of the cross-language wire protocol; this type must serialize to JSON
    /// with exactly these names.
    /// </para>
    /// </remarks>
    public class PollResult
    {
        /// <summary>
        /// Gets or sets the items matched by the poll.
        /// </summary>
        [JsonPropertyName("items")]
#pragma warning disable CA1002, CA2227 // Mutable DTO for the wire protocol; List is intended
        public List<WireItem> Items { get; set; } = new();
#pragma warning restore CA1002, CA2227

        /// <summary>
        /// Gets or sets the offset the next poll should start from.
        /// </summary>
        [JsonPropertyName("next_offset")]
        public long NextOffset { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the response was size-truncated and more items
        /// are ready immediately.
        /// </summary>
        [JsonPropertyName("more_ready")]
        public bool MoreReady { get; set; }
    }
}
