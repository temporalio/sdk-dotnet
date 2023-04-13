using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.SignalChildWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="Signal">Signal name.</param>
    /// <param name="Args">Signal arguments.</param>
    /// <param name="Options">Options if any.</param>
    /// <param name="Headers">Headers if any.</param>
    public record SignalChildWorkflowInput(
        string ID,
        string Signal,
        IReadOnlyCollection<object?> Args,
        ChildWorkflowSignalOptions? Options,
        IDictionary<string, Payload>? Headers);
}