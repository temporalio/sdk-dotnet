using System.Collections.Generic;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Publishes to and subscribes from a single topic. Obtained via
    /// <see cref="WorkflowStreamClient.Topic" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public sealed class TopicHandle
    {
        private readonly WorkflowStreamClient client;

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicHandle"/> class.
        /// </summary>
        /// <param name="name">Topic name.</param>
        /// <param name="client">Owning stream client.</param>
        internal TopicHandle(string name, WorkflowStreamClient client)
        {
            Name = name;
            this.client = client;
        }

        /// <summary>
        /// Gets the topic name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Buffers <paramref name="value" /> for publishing on this topic. The value goes through
        /// the client's payload converter immediately, on the caller's thread, so an
        /// unconvertible value fails this call rather than a later background flush; a pre-built
        /// <see cref="Temporalio.Api.Common.V1.Payload" /> bypasses conversion. Pass
        /// <paramref name="forceFlush" /> to wake the publisher and send immediately.
        /// </summary>
        /// <param name="value">Value to publish.</param>
        /// <param name="forceFlush">Wake the publisher and send immediately.</param>
        public void Publish(object? value, bool forceFlush = false) =>
            client.PublishToTopic(Name, value, forceFlush);

        /// <summary>
        /// Returns a subscription over items on this topic, starting at
        /// <paramref name="fromOffset" />. See
        /// <see cref="WorkflowStreamClient.Subscribe(SubscribeOptions?)" />.
        /// </summary>
        /// <param name="fromOffset">Global offset to start from. Zero means the beginning of
        /// whatever still exists.</param>
        /// <returns>The subscription.</returns>
#pragma warning disable VSTHRD200 // Name matches the other SDKs' workflow streams packages; subscribing is not itself async work
        public WorkflowStreamSubscription Subscribe(long fromOffset = 0) =>
#pragma warning restore VSTHRD200
            client.Subscribe(new SubscribeOptions
            {
                Topics = new List<string> { Name },
                FromOffset = fromOffset,
            });

        /// <summary>
        /// Subscribes <paramref name="listener" /> to items on this topic, starting at
        /// <paramref name="fromOffset" />, without occupying a caller thread. See
        /// <see cref="WorkflowStreamClient.Subscribe(SubscribeOptions, WorkflowStreamListener)" />.
        /// </summary>
        /// <param name="fromOffset">Global offset to start from.</param>
        /// <param name="listener">Listener receiving the items.</param>
        /// <returns>A handle controlling the subscription.</returns>
        public WorkflowStreamSubscriptionHandle Subscribe(long fromOffset, WorkflowStreamListener listener) =>
            client.Subscribe(
                new SubscribeOptions
                {
                    Topics = new List<string> { Name },
                    FromOffset = fromOffset,
                },
                listener);
    }
}
