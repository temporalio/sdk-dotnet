namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Publishes to a single topic from workflow code. Obtained via
    /// <see cref="WorkflowStream.Topic" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public sealed class WorkflowTopicHandle
    {
        private readonly WorkflowStream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTopicHandle"/> class.
        /// </summary>
        /// <param name="name">Topic name.</param>
        /// <param name="stream">Owning stream.</param>
        internal WorkflowTopicHandle(string name, WorkflowStream stream)
        {
            Name = name;
            this.stream = stream;
        }

        /// <summary>
        /// Gets the topic name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Appends <paramref name="value" /> to the stream on this topic. The value is serialized
        /// by the stream's payload converter (see
        /// <see cref="WorkflowStreamOptions.PayloadConverter" />), defaulting to the workflow's
        /// converter; a pre-built <see cref="Temporalio.Api.Common.V1.Payload" /> bypasses
        /// conversion. The item is appended directly to the log and is immediately visible to
        /// pollers.
        /// </summary>
        /// <param name="value">Value to publish.</param>
        public void Publish(object? value) => stream.PublishToTopic(Name, value);
    }
}
