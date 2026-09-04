namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>A workflow-side handle bound to one topic.</summary>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed class WorkflowTopicHandle
    {
        private readonly WorkflowStream stream;

        /// <summary>Initializes a new instance of the <see cref="WorkflowTopicHandle"/> class.</summary>
        /// <param name="stream">Workflow stream whose global log receives publications.</param>
        /// <param name="name">Normalized topic name.</param>
        internal WorkflowTopicHandle(WorkflowStream stream, string name)
        {
            this.stream = stream;
            Name = name ?? string.Empty;
        }

        /// <summary>Gets the topic name.</summary>
        public string Name { get; }

        /// <summary>Appends a value to the workflow's durable stream log.</summary>
        /// <param name="value">The value or pre-built Temporal payload to append.</param>
        public void Publish(object? value) => stream.Publish(Name, value);
    }
}
