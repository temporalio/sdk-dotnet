using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Workflows;

namespace Temporalio.Worker.Interceptors
{
    /// <summary>
    /// Input for <see cref="WorkflowOutboundInterceptor.CreateContinueAsNewException" />.
    /// </summary>
    /// <param name="Workflow">Workflow to continue.</param>
    /// <param name="Args">Arguments for the workflow.</param>
    /// <param name="Options">Options passed in to continue as new.</param>
    /// <param name="Headers">Headers to include.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CreateContinueAsNewExceptionInput(
        string Workflow,
        IReadOnlyCollection<object?> Args,
        ContinueAsNewOptions? Options,
        IDictionary<string, Payload>? Headers);
}