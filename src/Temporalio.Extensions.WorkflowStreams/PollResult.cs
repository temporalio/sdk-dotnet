using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>The cross-language result returned by the poll update.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed class PollResult
    {
        private IReadOnlyCollection<WireItem> items = Array.Empty<WireItem>();

        /// <summary>Gets or sets the items returned by this page.</summary>
        [JsonPropertyName("items")]
        public IReadOnlyCollection<WireItem> Items
        {
            get => items;
            set => items = value ?? Array.Empty<WireItem>();
        }

        /// <summary>Gets or sets the offset for the next poll.</summary>
        [JsonPropertyName("next_offset")]
        public long NextOffset { get; set; }

        /// <summary>Gets or sets a value indicating whether another page is immediately available.</summary>
        [JsonPropertyName("more_ready")]
        public bool MoreReady { get; set; }
    }
}
