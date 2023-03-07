using Temporalio.Api.WorkflowService.V1;
using Temporalio.Converters;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a workflow execution and description.
    /// </summary>
    public class WorkflowExecutionDescription : WorkflowExecution
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowExecutionDescription"/> class.
        /// </summary>
        /// <param name="rawDescription">Raw description response.</param>
        /// <param name="dataConverter">Data converter for memos.</param>
        public WorkflowExecutionDescription(
            DescribeWorkflowExecutionResponse rawDescription, DataConverter dataConverter)
            : base(rawDescription.WorkflowExecutionInfo, dataConverter) => RawDescription = rawDescription;

        /// <summary>
        /// Gets the raw proto info.
        /// </summary>
        public DescribeWorkflowExecutionResponse RawDescription { get; private init; }
    }
}