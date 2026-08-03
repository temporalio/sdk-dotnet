using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// A single entry within a publish batch on the wire. <see cref="Data" /> is a base64-encoded,
    /// serialized <see cref="Temporalio.Api.Common.V1.Payload" />.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change.
    /// <para>
    /// Field names are part of the cross-language wire protocol; this type must serialize to JSON
    /// with exactly these names.
    /// </para>
    /// </remarks>
    public class PublishEntry
    {
        /// <summary>
        /// Gets or sets the topic this entry is published on. Null means no topic.
        /// </summary>
        [JsonPropertyName("topic")]
        public string? Topic { get; set; }

        /// <summary>
        /// Gets or sets the base64-encoded, serialized
        /// <see cref="Temporalio.Api.Common.V1.Payload" />.
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
}
