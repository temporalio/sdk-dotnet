using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// A strongly typed client-side handle bound to one topic.
    /// </summary>
    /// <typeparam name="T">
    /// Type of values published to and received from the topic.
    /// </typeparam>
    /// <remarks>
    /// WARNING: Workflow Streams is experimental and may change.
    /// </remarks>
    public sealed class WorkflowStreamClientTopicHandle<T>
    {
        private readonly WorkflowStreamClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStreamClientTopicHandle{T}"/> class.
        /// </summary>
        /// <param name="client">
        /// Client whose publisher and lifecycle the handle shares.
        /// </param>
        /// <param name="name">
        /// Normalized topic name.
        /// </param>
        internal WorkflowStreamClientTopicHandle(WorkflowStreamClient client, string name)
        {
            this.client = client;
            Name = name ?? string.Empty;
        }

        /// <summary>
        /// Gets the topic name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Converts and buffers a value for publication on this topic.
        /// </summary>
        /// <param name="value">
        /// The value to publish.
        /// </param>
        /// <param name="forceFlush">
        /// Whether to wake the asynchronous flusher immediately.
        /// </param>
        public void Publish(T value, bool forceFlush = false) =>
            client.Publish(Name, value, forceFlush);

        /// <summary>
        /// Buffers a pre-built Temporal payload for publication on this topic.
        /// </summary>
        /// <param name="payload">
        /// The payload to publish.
        /// </param>
        /// <param name="forceFlush">
        /// Whether to wake the asynchronous flusher immediately.
        /// </param>
        public void Publish(Payload payload, bool forceFlush = false) =>
            client.Publish(Name, payload, forceFlush);

        /// <summary>
        /// Creates a reusable subscription to this topic.
        /// </summary>
        /// <param name="fromOffset">
        /// The global offset at which to begin.
        /// </param>
        /// <returns>
        /// A reusable asynchronous stream.
        /// </returns>
        public IAsyncEnumerable<WorkflowStreamItem<T>> SubscribeAsync(long fromOffset = 0) =>
            client.SubscribeAsync<T>(new()
            {
                Topics = new[] { Name },
                FromOffset = fromOffset,
            });
    }
}
