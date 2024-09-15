using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.StartChildWorkflowAsync{TWorkflow, TResult}(StartChildWorkflowInput)" />.
    /// </summary>
    /// <param name="Workflow">Workflow type name.</param>
    /// <param name="Args">Workflow args.</param>
    /// <param name="Options">Workflow options.</param>
    /// <param name="Headers">Headers if any. These will be encoded using the codec before sent
    /// to the server.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record StartChildWorkflowInput(
        string Workflow,
        IReadOnlyCollection<object?> Args,
        ChildWorkflowOptions Options,
        IDictionary<string, Payload>? Headers);
}