using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// The wire representation of a stream item. <see cref="Data" /> is a base64-encoded,
    /// serialized <see cref="Temporalio.Api.Common.V1.Payload" />.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Field names are part of the cross-language wire protocol; this type must serialize to JSON
    /// with exactly these names.
    /// </para>
    /// </remarks>
    public class WireItem
    {
        /// <summary>
        /// Gets or sets the topic this item was published on. Null means no topic.
        /// </summary>
        [JsonPropertyName("topic")]
        public string? Topic { get; set; }

        /// <summary>
        /// Gets or sets the base64-encoded, serialized
        /// <see cref="Temporalio.Api.Common.V1.Payload" />.
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; }

        /// <summary>
        /// Gets or sets the item's offset, global across topics.
        /// </summary>
        [JsonPropertyName("offset")]
        public long Offset { get; set; }
    }
}
