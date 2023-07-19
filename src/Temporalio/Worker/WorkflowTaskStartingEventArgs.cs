namespace Temporalio.Worker
{
    /// <summary>
    /// Event arguments for workflow task starting events.
    /// </summary>
    /// <remarks>
    /// WARNING: This is experimental and there are many caveats about its use. It is important to
    /// read the documentation on <see cref="TemporalWorkerOptions.WorkflowTaskStarting" />.
    /// </remarks>
    public class WorkflowTaskStartingEventArgs : WorkflowTaskEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTaskStartingEventArgs"/> class.
        /// </summary>
        /// <param name="workflowInstance">Workflow instance.</param>
        internal WorkflowTaskStartingEventArgs(WorkflowInstance workflowInstance)
            : base(workflowInstance)
        {
        }
    }
}