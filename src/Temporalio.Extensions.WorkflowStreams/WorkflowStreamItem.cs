using Temporalio.Api.Common.V1;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>A decoded item yielded by a Workflow Streams subscription.</summary>
    /// <param name="Topic">The item's topic, or an empty string for no topic.</param>
    /// <param name="Payload">The raw Temporal payload.</param>
    /// <param name="Offset">The item's global offset.</param>
    /// <remarks>WARNING: Workflow Streams is experimental and may change.</remarks>
    public sealed record WorkflowStreamItem(string Topic, Payload Payload, long Offset);
}
