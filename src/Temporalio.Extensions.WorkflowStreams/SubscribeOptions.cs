using System;
using System.Collections.Generic;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Options for <see cref="WorkflowStreamClient.Subscribe(SubscribeOptions?)" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public class SubscribeOptions
    {
        /// <summary>
        /// Gets or sets the topics to filter the subscription on. Empty (the default) means all
        /// topics.
        /// </summary>
#pragma warning disable CA2227 // Options objects are mutable by design in this SDK
        public IList<string> Topics { get; set; } = new List<string>();
#pragma warning restore CA2227

        /// <summary>
        /// Gets or sets the global offset to start from. Zero (the default) means the beginning
        /// of whatever still exists.
        /// </summary>
        public long FromOffset { get; set; }

        /// <summary>
        /// Gets or sets the minimum interval between polls when no more items are immediately
        /// ready. Default: 100 milliseconds.
        /// </summary>
        public TimeSpan PollCooldown { get; set; } = WorkflowStreamConstants.DefaultPollCooldown;
    }
}
