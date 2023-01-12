using System;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception representing a child workflow failure.
    /// </summary>
    public class ChildWorkflowFailureException : FailureException
    {
        /// <inheritdoc />
        protected internal ChildWorkflowFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.ChildWorkflowExecutionFailureInfo == null)
            {
                throw new ArgumentException("Missing child workflow failure info");
            }
        }

        /// <inheritdoc />
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Namespace of the failed child workflow.
        /// </summary>
        public string Namespace => Failure!.ChildWorkflowExecutionFailureInfo.Namespace;

        /// <summary>
        /// ID of the failed child workflow.
        /// </summary>
        public string WorkflowID =>
            Failure!.ChildWorkflowExecutionFailureInfo.WorkflowExecution.WorkflowId;

        /// <summary>
        /// Run ID of the failed child workflow.
        /// </summary>
        public string RunID => Failure!.ChildWorkflowExecutionFailureInfo.WorkflowExecution.RunId;

        /// <summary>
        /// Child workflow name or "type" that failed.
        /// </summary>
        public string WorkflowType => Failure!.ChildWorkflowExecutionFailureInfo.WorkflowType.Name;

        /// <summary>
        /// Retry state of the failure.
        /// </summary>
        public RetryState RetryState => Failure!.ChildWorkflowExecutionFailureInfo.RetryState;
    }
}
