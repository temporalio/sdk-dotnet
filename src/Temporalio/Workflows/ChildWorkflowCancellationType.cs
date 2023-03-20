namespace Temporalio.Workflows
{
    /// <summary>
    /// How a child workflow cancellation is treated by the workflow.
    /// </summary>
    public enum ChildWorkflowCancellationType
    {
        /// <summary>
        /// Do not request cancellation of the child workflow is already scheduled.
        /// </summary>
        Abandon = Bridge.Api.ChildWorkflow.ChildWorkflowCancellationType.Abandon,

        /// <summary>
        /// Initiate a cancellation request and immediately report cancellation to the parent.
        /// </summary>
        TryCancel = Bridge.Api.ChildWorkflow.ChildWorkflowCancellationType.TryCancel,

        /// <summary>
        /// Wait for child cancellation completion.
        /// </summary>
        WaitCancellationCompleted = Bridge.Api.ChildWorkflow.ChildWorkflowCancellationType.WaitCancellationCompleted,

        /// <summary>
        /// Request cancellation of the child and wait for confirmation that the request was
        /// received.
        /// </summary>
        WaitCancellationRequested = Bridge.Api.ChildWorkflow.ChildWorkflowCancellationType.WaitCancellationRequested,
    }
}