namespace Temporalio.Workflows
{
    /// <summary>
    /// How a workflow child will be handled when its parent workflow closes.
    /// </summary>
    public enum ParentClosePolicy
    {
        /// <summary>
        /// No value set and will internally default. This should not be used.
        /// </summary>
        None = Bridge.Api.ChildWorkflow.ParentClosePolicy.Unspecified,

        /// <summary>
        /// Child workflow will be terminated.
        /// </summary>
        Terminate = Bridge.Api.ChildWorkflow.ParentClosePolicy.Terminate,

        /// <summary>
        /// Child workflow will do nothing.
        /// </summary>
        Abandon = Bridge.Api.ChildWorkflow.ParentClosePolicy.Abandon,

        /// <summary>
        /// Cancellation will be requested on the child workflow.
        /// </summary>
        RequestCancel = Bridge.Api.ChildWorkflow.ParentClosePolicy.RequestCancel,
    }
}