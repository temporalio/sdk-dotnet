using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.ToolRegistry
{
    /// <summary>
    /// Heartbeat payload for crash-safe session recovery. Serialized to Temporal heartbeat details
    /// on each turn; deserialized on activity retry.
    /// </summary>
    internal sealed class SessionCheckpoint
    {
        /// <summary>
        /// Gets or sets the checkpoint schema version. Absent (0) in pre-versioned checkpoints.
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Gets or sets the conversation message history.
        /// </summary>
        [JsonPropertyName("messages")]
        public List<Dictionary<string, object?>>? Messages { get; set; } = new();

        /// <summary>
        /// Gets or sets accumulated application-level results from tool calls.
        /// </summary>
        [JsonPropertyName("results")]
        public List<Dictionary<string, object?>>? Results { get; set; } = new();
    }
}
