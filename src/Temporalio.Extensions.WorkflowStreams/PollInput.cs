using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>The cross-language update input for polling stream items.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed class PollInput
    {
        private IReadOnlyCollection<string> topics = Array.Empty<string>();

        /// <summary>Gets or sets the topic filter. Empty means all topics.</summary>
        [JsonPropertyName("topics")]
        public IReadOnlyCollection<string> Topics
        {
            get => topics;
            set => topics = value?.Select(topic => topic ?? string.Empty).ToArray() ??
                Array.Empty<string>();
        }

        /// <summary>Gets or sets the global offset at which polling begins.</summary>
        [JsonPropertyName("from_offset")]
        public long FromOffset { get; set; }
    }
}
