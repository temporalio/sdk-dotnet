using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.StartUpdateWithStartWorkflowAsync" />.
    /// </summary>
    /// <param name="Update">Update name.</param>
    /// <param name="Args">Update arguments.</param>
    /// <param name="Options">Options.</param>
    /// <param name="Headers">Headers if any for the update. These will be encoded using the codec
    /// before sent to the server. Note these are the update headers, start headers are in the start
    /// operation.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    /// <remarks>WARNING: Workflow update with start is experimental and APIs may change.</remarks>
    public record StartUpdateWithStartWorkflowInput(
        string Update,
        IReadOnlyCollection<object?> Args,
        WorkflowStartUpdateWithStartOptions Options,
        IDictionary<string, Payload>? Headers);
}