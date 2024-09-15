using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.StartWorkflowAsync{TWorkflow,TResult}" />.
    /// </summary>
    /// <param name="Workflow">Workflow type name to start.</param>
    /// <param name="Args">Arguments for the workflow.</param>
    /// <param name="Options">Options passed in to start.</param>
    /// <param name="Headers">Headers to include for workflow start. These will be encoded using the
    /// codec before sent to the server.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record StartWorkflowInput(
        string Workflow,
        IReadOnlyCollection<object?> Args,
        WorkflowOptions Options,
        IDictionary<string, Payload>? Headers);
}