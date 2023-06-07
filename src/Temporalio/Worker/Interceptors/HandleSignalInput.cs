using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowInboundInterceptor.HandleSignalAsync" />.
    /// </summary>
    /// <param name="Signal">Signal name.</param>
    /// <param name="Definition">Signal definition.</param>
    /// <param name="Args">Signal arguments.</param>
    /// <param name="Headers">Signal headers.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record HandleSignalInput(
        string Signal,
        WorkflowSignalDefinition Definition,
        object?[] Args,
        IReadOnlyDictionary<string, Payload>? Headers);
}