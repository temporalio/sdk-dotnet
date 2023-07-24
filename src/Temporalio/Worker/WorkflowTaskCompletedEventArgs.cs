using System;

namespace Temporalio.Worker
{
    /// <summary>
    /// Event arguments for workflow task completed events.
    /// </summary>
    /// <remarks>
    /// WARNING: This is experimental and there are many caveats about its use. It is important to
    /// read the documentation on <see cref="TemporalWorkerOptions.WorkflowTaskStarting" />.
    /// </remarks>
    public class WorkflowTaskCompletedEventArgs : WorkflowTaskEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTaskCompletedEventArgs"/> class.
        /// </summary>
        /// <param name="workflowInstance">Workflow instance.</param>
        /// <param name="taskFailureException">Task failure exception.</param>
        internal WorkflowTaskCompletedEventArgs(
            WorkflowInstance workflowInstance, Exception? taskFailureException)
            : base(workflowInstance) => TaskFailureException = taskFailureException;

        /// <summary>
        /// Gets the task failure if any.
        /// </summary>
        /// <remarks>
        /// This is the task failure not the workflow failure. Task failures occur on all exceptions
        /// except Temporal exceptions thrown from the workflow. These cause the workflow to
        /// continually retry until code is fixed to solve the exception.
        /// </remarks>
        public Exception? TaskFailureException { get; private init; }
    }
}