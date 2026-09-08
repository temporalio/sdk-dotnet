using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>Options for a Workflow Streams subscription.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public class WorkflowStreamSubscribeOptions : ICloneable
    {
        private IReadOnlyCollection<string> topics = Array.Empty<string>();

        /// <summary>Gets or sets the topic filter. Empty means all topics.</summary>
        public IReadOnlyCollection<string> Topics
        {
            get => topics;
            set => topics = value?.Select(topic => topic ?? string.Empty).ToArray() ??
                Array.Empty<string>();
        }

        /// <summary>Gets or sets the global offset at which the subscription begins.</summary>
        public long FromOffset { get; set; }

        /// <summary>Gets or sets the delay between polls when no page is immediately ready.</summary>
        public TimeSpan PollCooldown { get; set; } = WorkflowStreamConstants.DefaultPollCooldown;

        /// <summary>Creates a copy of these options, including the topic collection.</summary>
        /// <returns>A copied options instance.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowStreamSubscribeOptions)MemberwiseClone();
            copy.topics = topics.ToArray();
            return copy;
        }
    }
}
