using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>The cross-language signal input for publishing a batch.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed class PublishInput
    {
        private IReadOnlyCollection<PublishEntry> items = Array.Empty<PublishEntry>();
        private string publisherId = string.Empty;

        /// <summary>Gets or sets the entries in the batch.</summary>
        [JsonPropertyName("items")]
        public IReadOnlyCollection<PublishEntry> Items
        {
            get => items;
            set => items = value ?? Array.Empty<PublishEntry>();
        }

        /// <summary>Gets or sets the stable publisher identifier used for deduplication.</summary>
        [JsonPropertyName("publisher_id")]
        public string PublisherId
        {
            get => publisherId;
            set => publisherId = value ?? string.Empty;
        }

        /// <summary>Gets or sets the publisher-local batch sequence.</summary>
        [JsonPropertyName("sequence")]
        public long Sequence { get; set; }
    }
}
