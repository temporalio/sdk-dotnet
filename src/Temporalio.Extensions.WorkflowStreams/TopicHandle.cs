using System.Collections.Generic;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>A client-side handle bound to one topic.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed class TopicHandle
    {
        private readonly WorkflowStreamClient client;

        /// <summary>Initializes a new instance of the <see cref="TopicHandle"/> class.</summary>
        /// <param name="client">Client whose publisher and lifecycle the handle shares.</param>
        /// <param name="name">Normalized topic name.</param>
        internal TopicHandle(WorkflowStreamClient client, string name)
        {
            this.client = client;
            Name = name ?? string.Empty;
        }

        /// <summary>Gets the topic name.</summary>
        public string Name { get; }

        /// <summary>Converts and buffers a value for publication on this topic.</summary>
        /// <param name="value">The value or pre-built Temporal payload to publish.</param>
        /// <param name="forceFlush">Whether to wake the asynchronous flusher immediately.</param>
        public void Publish(object? value, bool forceFlush = false) =>
            client.Publish(Name, value, forceFlush);

        /// <summary>Creates a reusable subscription to this topic.</summary>
        /// <param name="fromOffset">The global offset at which to begin.</param>
        /// <returns>A reusable asynchronous stream.</returns>
        public IAsyncEnumerable<WorkflowStreamItem> SubscribeAsync(long fromOffset = 0) =>
            client.SubscribeAsync(new()
            {
                Topics = new[] { Name },
                FromOffset = fromOffset,
            });
    }
}
