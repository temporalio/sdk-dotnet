namespace Temporalio.Workflows
{
    /// <summary>
    /// How an activity cancellation is treated by the workflow.
    /// </summary>
    public enum ActivityCancellationType
    {
        /// <summary>
        /// Initiate a cancellation request and immediately report cancellation to the workflow.
        /// </summary>
        TryCancel = Bridge.Api.WorkflowCommands.ActivityCancellationType.TryCancel,

        /// <summary>
        /// Wait for activity cancellation completion. Note that activity must heartbeat to receive
        /// a cancellation notification.
        /// </summary>
        WaitCancellationCompleted = Bridge.Api.WorkflowCommands.ActivityCancellationType.WaitCancellationCompleted,

        /// <summary>
        /// Do not request cancellation of the activity and immediately report cancellation to the
        /// workflow.
        /// </summary>
        Abandon = Bridge.Api.WorkflowCommands.ActivityCancellationType.Abandon,
    }
}