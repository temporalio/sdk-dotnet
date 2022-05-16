using Temporal.Api.Enums.V1;
using Xunit;

namespace Temporal.Sdk.Common.Tests
{

    public class TestWorkflowExecutionStatusExtensions
    {
        [Theory]
        [InlineData(WorkflowExecutionStatus.Unspecified, false)]
        [InlineData(WorkflowExecutionStatus.Running, false)]
        [InlineData(WorkflowExecutionStatus.Completed, true)]
        [InlineData(WorkflowExecutionStatus.Failed, true)]
        [InlineData(WorkflowExecutionStatus.Canceled, true)]
        [InlineData(WorkflowExecutionStatus.Terminated, true)]
        [InlineData(WorkflowExecutionStatus.ContinuedAsNew, true)]
        [InlineData(WorkflowExecutionStatus.TimedOut, true)]
        public void Test_IsTerminal_Returns_Correct_Result(WorkflowExecutionStatus status, bool expected)
        {
            Assert.Equal(expected, status.IsTerminal());
        }
    }
}