using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.QueryWorkflowAsync{TResult}(QueryWorkflowInput)" />.
    /// </summary>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">Workflow run ID if any.</param>
    /// <param name="Query">Query name.</param>
    /// <param name="Args">Query arguments.</param>
    /// <param name="Options">Options if any.</param>
    /// <param name="Headers">Headers if any. These will be encoded using the codec before sent
    /// to the server.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record QueryWorkflowInput(
        string Id,
        string? RunId,
        string Query,
        IReadOnlyCollection<object?> Args,
        WorkflowQueryOptions? Options,
        IDictionary<string, Payload>? Headers);
}