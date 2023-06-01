using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.SignalExternalWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">Workflow run ID if any.</param>
    /// <param name="Signal">Signal name.</param>
    /// <param name="Args">Signal arguments.</param>
    /// <param name="Options">Options if any.</param>
    /// <param name="Headers">Headers if any.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record SignalExternalWorkflowInput(
        string ID,
        string? RunID,
        string Signal,
        IReadOnlyCollection<object?> Args,
        ExternalWorkflowSignalOptions? Options,
        IDictionary<string, Payload>? Headers);
}