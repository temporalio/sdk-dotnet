using Temporalio.Api.Workflow.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a workflow execution.
    /// </summary>
    /// <param name="RawInfo">Underlying protobuf information.</param>
    public record WorkflowExecution(WorkflowExecutionInfo RawInfo)
    {
        // TODO(cretz): High level property getters
    }
}