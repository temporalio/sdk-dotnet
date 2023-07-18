using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.SignalWorkflowAsync" />.
    /// </summary>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">Workflow run ID if any.</param>
    /// <param name="Signal">Signal name.</param>
    /// <param name="Args">Signal arguments.</param>
    /// <param name="Options">Options if any.</param>
    /// <param name="Headers">Headers if any. These will be encoded using the codec before sent
    /// to the server.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record SignalWorkflowInput(
        string Id,
        string? RunId,
        string Signal,
        IReadOnlyCollection<object?> Args,
        WorkflowSignalOptions? Options,
        IDictionary<string, Payload>? Headers);
}