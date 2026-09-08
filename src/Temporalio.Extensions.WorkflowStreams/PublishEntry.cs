using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>A single item in a publish signal batch.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed class PublishEntry
    {
        private string topic = string.Empty;
        private string data = string.Empty;

        /// <summary>Gets or sets the topic. A wire-level null is normalized to an empty string.</summary>
        [JsonPropertyName("topic")]
        public string Topic
        {
            get => topic;
            set => topic = value ?? string.Empty;
        }

        /// <summary>Gets or sets the base64-encoded serialized Temporal payload.</summary>
        [JsonPropertyName("data")]
        public string Data
        {
            get => data;
            set => data = value ?? string.Empty;
        }
    }
}
