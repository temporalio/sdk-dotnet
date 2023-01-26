using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.QueryWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">Workflow run ID if any.</param>
    /// <param name="Query">Query name.</param>
    /// <param name="Args">Query arguments.</param>
    /// <param name="Options">Options if any.</param>
    /// <param name="Headers">Headers if any.</param>
    public record QueryWorkflowInput(
        string ID,
        string? RunID,
        string Query,
        IReadOnlyCollection<object?> Args,
        WorkflowQueryOptions? Options,
        IDictionary<string, Payload>? Headers);
}