using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.StartChildWorkflowAsync" />.
    /// </summary>
    /// <param name="Workflow">Workflow type name.</param>
    /// <param name="Args">Workflow args.</param>
    /// <param name="Options">Workflow options.</param>
    /// <param name="Headers">Headers.</param>
    public record StartChildWorkflowInput(
        string Workflow,
        IReadOnlyCollection<object?> Args,
        ChildWorkflowOptions Options,
        IDictionary<string, Payload>? Headers);
}