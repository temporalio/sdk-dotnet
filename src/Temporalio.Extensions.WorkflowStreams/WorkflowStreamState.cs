using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>Serializable Workflow Streams state for continue-as-new.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed class WorkflowStreamState
    {
        private IReadOnlyCollection<WireItem> log = Array.Empty<WireItem>();
        private IReadOnlyDictionary<string, long> publisherSequences =
            new Dictionary<string, long>();

        private IReadOnlyDictionary<string, double> publisherLastSeen =
            new Dictionary<string, double>();

        /// <summary>Gets or sets the retained stream log.</summary>
        [JsonPropertyName("log")]
        public IReadOnlyCollection<WireItem> Log
        {
            get => log;
            set => log = value ?? Array.Empty<WireItem>();
        }

        /// <summary>Gets or sets the global offset represented by the start of <see cref="Log"/>.</summary>
        [JsonPropertyName("base_offset")]
        public long BaseOffset { get; set; }

        /// <summary>Gets or sets the most recently observed sequence for each publisher.</summary>
        [JsonPropertyName("publisher_sequences")]
        public IReadOnlyDictionary<string, long> PublisherSequences
        {
            get => publisherSequences;
            set => publisherSequences = value ?? new Dictionary<string, long>();
        }

        /// <summary>Gets or sets each publisher's last-seen Unix timestamp in seconds.</summary>
        [JsonPropertyName("publisher_last_seen")]
        public IReadOnlyDictionary<string, double> PublisherLastSeen
        {
            get => publisherLastSeen;
            set => publisherLastSeen = value ?? new Dictionary<string, double>();
        }
    }
}
