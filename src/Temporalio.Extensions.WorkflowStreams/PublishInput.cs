using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// The publish signal payload carrying a batch of entries, along with the dedup fields.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Field names are part of the cross-language wire protocol; this type must serialize to JSON
    /// with exactly these names.
    /// </para>
    /// </remarks>
    public class PublishInput
    {
        /// <summary>
        /// Gets or sets the batch of entries to append.
        /// </summary>
        [JsonPropertyName("items")]
#pragma warning disable CA1002, CA2227 // Mutable DTO for the wire protocol; List is intended
        public List<PublishEntry> Items { get; set; } = new();
#pragma warning restore CA1002, CA2227

        /// <summary>
        /// Gets or sets the dedup key identifying the publisher. Empty bypasses dedup.
        /// </summary>
        [JsonPropertyName("publisher_id")]
        public string PublisherId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the publisher's monotonic batch sequence, used for dedup.
        /// </summary>
        [JsonPropertyName("sequence")]
        public long Sequence { get; set; }
    }
}
