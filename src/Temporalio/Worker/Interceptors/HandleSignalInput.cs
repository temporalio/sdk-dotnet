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
    public record HandleSignalInput(
        string Signal,
        WorkflowSignalDefinition Definition,
        object?[] Args,
        IDictionary<string, Payload>? Headers);
}