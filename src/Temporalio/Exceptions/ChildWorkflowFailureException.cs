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
        /// <summary>
        /// Initializes a new instance of the <see cref="ChildWorkflowFailureException"/> class.
        /// </summary>
        /// <param name="failure">Underlying proto failure.</param>
        /// <param name="inner">Inner exception if any.</param>
        internal protected ChildWorkflowFailureException(Failure failure, Exception? inner)
            : base(failure, inner)
        {
            if (failure.ChildWorkflowExecutionFailureInfo == null)
            {
                throw new ArgumentException("Missing child workflow failure info");
            }
        }

        /// <summary>
        /// Gets the underlying protobuf failure object.
        /// </summary>
        public new Failure Failure => base.Failure!;

        /// <summary>
        /// Gets the namespace of the failed child workflow.
        /// </summary>
        public string Namespace => Failure!.ChildWorkflowExecutionFailureInfo.Namespace;

        /// <summary>
        /// Gets the ID of the failed child workflow.
        /// </summary>
        public string WorkflowID =>
            Failure!.ChildWorkflowExecutionFailureInfo.WorkflowExecution.WorkflowId;

        /// <summary>
        /// Gets the run ID of the failed child workflow.
        /// </summary>
        public string RunID => Failure!.ChildWorkflowExecutionFailureInfo.WorkflowExecution.RunId;

        /// <summary>
        /// Gets the child workflow name or "type" that failed.
        /// </summary>
        public string WorkflowType => Failure!.ChildWorkflowExecutionFailureInfo.WorkflowType.Name;

        /// <summary>
        /// Gets the retry state of the failure.
        /// </summary>
        public RetryState RetryState => Failure!.ChildWorkflowExecutionFailureInfo.RetryState;
    }
}
