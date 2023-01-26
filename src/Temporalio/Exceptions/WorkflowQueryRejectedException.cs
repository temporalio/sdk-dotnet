using Temporalio.Api.Enums.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Thrown when a query is rejected by the worker due to bad workflow status.
    /// </summary>
    public class WorkflowQueryRejectedException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowQueryRejectedException"/> class.
        /// </summary>
        /// <param name="workflowStatus">Workflow status causing rejection.</param>
        public WorkflowQueryRejectedException(WorkflowExecutionStatus workflowStatus)
            : base($"Query rejected, workflow status: {workflowStatus}")
        {
            WorkflowStatus = workflowStatus;
        }

        /// <summary>
        /// Gets the workflow status causing this rejection.
        /// </summary>
        public WorkflowExecutionStatus WorkflowStatus { get; private init; }
    }
}