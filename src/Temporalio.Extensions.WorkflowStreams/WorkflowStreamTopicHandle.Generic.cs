using Temporalio.Api.Common.V1;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// A strongly typed workflow-side handle bound to one topic.
    /// </summary>
    /// <typeparam name="T">
    /// Type of values published to the topic.
    /// </typeparam>
    /// <remarks>
    /// WARNING: Workflow Streams is experimental and may change.
    /// </remarks>
    public sealed class WorkflowStreamTopicHandle<T>
    {
        private readonly WorkflowStream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStreamTopicHandle{T}"/> class.
        /// </summary>
        /// <param name="stream">
        /// Workflow stream whose global log receives publications.
        /// </param>
        /// <param name="name">
        /// Normalized topic name.
        /// </param>
        internal WorkflowStreamTopicHandle(WorkflowStream stream, string name)
        {
            this.stream = stream;
            Name = name ?? string.Empty;
        }

        /// <summary>
        /// Gets the topic name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Appends a value to the workflow's durable stream log.
        /// </summary>
        /// <param name="value">
        /// The value to append.
        /// </param>
        public void Publish(T value) => stream.Publish(Name, value);

        /// <summary>
        /// Appends a pre-built Temporal payload to the workflow's durable stream log.
        /// </summary>
        /// <param name="payload">
        /// The payload to append.
        /// </param>
        public void Publish(Payload payload) => stream.Publish(Name, payload);
    }
}
