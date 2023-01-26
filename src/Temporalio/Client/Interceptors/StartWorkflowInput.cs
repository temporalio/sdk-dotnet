using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.StartWorkflowAsync" />.
    /// </summary>
    /// <param name="Workflow">Workflow type name to start.</param>
    /// <param name="Args">Arguments for the workflow.</param>
    /// <param name="Options">Options passed in to start.</param>
    /// <param name="Headers">Headers to include for workflow start.</param>
    public record StartWorkflowInput(
        string Workflow,
        IReadOnlyCollection<object?> Args,
        WorkflowStartOptions Options,
        IDictionary<string, Payload>? Headers);
}