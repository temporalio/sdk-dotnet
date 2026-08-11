using Temporalio.Converters;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>
    /// Options for constructing a <see cref="WorkflowStream" />.
    /// </summary>
    /// <remarks>WARNING: This API is experimental and may change.</remarks>
    public class WorkflowStreamOptions
    {
        /// <summary>
        /// Gets or sets the converter that serializes values published from workflow code (via
        /// <see cref="WorkflowTopicHandle.Publish" />) into per-item payloads. If null, the
        /// workflow's <see cref="Workflows.Workflow.PayloadConverter" /> is used.
        /// </summary>
        /// <remarks>
        /// Only payload conversion happens here — never a payload codec. The worker's codec chain
        /// runs once on the poll-update response that carries each batch to subscribers, so
        /// encoding items here too would double-encode them; the
        /// <see cref="IPayloadConverter" /> type makes that impossible.
        /// </remarks>
        public IPayloadConverter? PayloadConverter { get; set; }
    }
}
