using Temporalio.Api.Common.V1;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// A single decoded item yielded by a subscription. <see cref="Payload" /> is the raw
    /// <see cref="Api.Common.V1.Payload" />; decode it at the call site with a payload converter,
    /// e.g. <c>converter.ToValue&lt;string&gt;(item.Payload)</c> (see
    /// <see cref="Converters.ConverterExtensions.ToValue{T}(Converters.IPayloadConverter, Payload)" />).
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public sealed class WorkflowStreamItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowStreamItem"/> class.
        /// </summary>
        /// <param name="topic">Topic the item was published on, or empty for no topic.</param>
        /// <param name="payload">The item's raw payload.</param>
        /// <param name="offset">The item's offset, global across topics.</param>
        public WorkflowStreamItem(string topic, Payload payload, long offset)
        {
            Topic = topic ?? string.Empty;
            Payload = payload;
            Offset = offset;
        }

        /// <summary>
        /// Gets the topic the item was published on, or empty for no topic.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the item's raw payload.
        /// </summary>
        public Payload Payload { get; }

        /// <summary>
        /// Gets the item's offset, global across topics.
        /// </summary>
        public long Offset { get; }
    }
}
