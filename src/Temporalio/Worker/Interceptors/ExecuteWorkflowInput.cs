using System.Collections.Generic;
using System.Reflection;
using Temporalio.Api.Common.V1;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowInboundInterceptor.ExecuteWorkflowAsync" />.
    /// </summary>
    /// <param name="Instance">Workflow instance.</param>
    /// <param name="RunMethod">Method to invoke on the instance.</param>
    /// <param name="Parameters">Run method parameters.</param>
    /// <param name="Headers">Workflow headers.</param>
    public record ExecuteWorkflowInput(
        object Instance,
        MethodInfo RunMethod,
        object?[] Parameters,
        IDictionary<string, Payload>? Headers);
}