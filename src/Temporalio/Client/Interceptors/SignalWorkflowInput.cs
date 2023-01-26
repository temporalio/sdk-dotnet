using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.SignalWorkflowAsync" />.
    /// </summary>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">Workflow run ID if any.</param>
    /// <param name="Signal">Signal name.</param>
    /// <param name="Args">Signal arguments.</param>
    /// <param name="Options">Options if any.</param>
    /// <param name="Headers">Headers if any.</param>
    public record SignalWorkflowInput(
        string ID,
        string? RunID,
        string Signal,
        IReadOnlyCollection<object?> Args,
        WorkflowSignalOptions? Options,
        IDictionary<string, Payload>? Headers);
}