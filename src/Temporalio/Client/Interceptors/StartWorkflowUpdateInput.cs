using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.StartWorkflowUpdateAsync{TResult}" />.
    /// </summary>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">Workflow run ID if any.</param>
    /// <param name="FirstExecutionRunId">Workflow first execution run ID if any.</param>
    /// <param name="Update">Update name.</param>
    /// <param name="Args">Update arguments.</param>
    /// <param name="Options">Options.</param>
    /// <param name="Headers">Headers if any. These will be encoded using the codec before sent
    /// to the server.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record StartWorkflowUpdateInput(
        string Id,
        string? RunId,
        string? FirstExecutionRunId,
        string Update,
        IReadOnlyCollection<object?> Args,
        WorkflowUpdateStartOptions Options,
        IDictionary<string, Payload>? Headers);
}