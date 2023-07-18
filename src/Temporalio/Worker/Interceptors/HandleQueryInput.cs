using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowInboundInterceptor.HandleQuery" />.
    /// </summary>
    /// <param name="Id">Query ID.</param>
    /// <param name="Query">Query name.</param>
    /// <param name="Definition">Query definition.</param>
    /// <param name="Args">Query arguments.</param>
    /// <param name="Headers">Query headers.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record HandleQueryInput(
        string Id,
        string Query,
        WorkflowQueryDefinition Definition,
        object?[] Args,
        IReadOnlyDictionary<string, Payload>? Headers);
}