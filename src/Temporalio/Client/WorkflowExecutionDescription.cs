using Temporalio.Api.WorkflowService.V1;

namespace Temporalio.Client
{
    public record WorkflowExecutionDescription(DescribeWorkflowExecutionResponse RawDescription) : WorkflowExecution(RawDescription.WorkflowExecutionInfo)
    {
        
    }
}