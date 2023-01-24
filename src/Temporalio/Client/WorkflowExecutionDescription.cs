using Temporalio.Api.WorkflowService.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a workflow execution and description.
    /// </summary>
    /// <param name="RawDescription">Underlying protobuf description.</param>
    public record WorkflowExecutionDescription(DescribeWorkflowExecutionResponse RawDescription) :
        WorkflowExecution(RawDescription.WorkflowExecutionInfo)
    {
    }
}