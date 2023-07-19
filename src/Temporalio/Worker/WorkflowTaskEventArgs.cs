using System;

namespace Temporalio.Worker
{
    /// <summary>
    /// Base class for workflow task event arguments.
    /// </summary>
    /// <remarks>
    /// WARNING: This is experimental and there are many caveats about its use. It is important to
    /// read the documentation on <see cref="TemporalWorkerOptions.WorkflowTaskStarting" />.
    /// </remarks>
    public abstract class WorkflowTaskEventArgs : EventArgs
    {
        private readonly WorkflowInstance workflowInstance;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTaskEventArgs"/> class.
        /// </summary>
        /// <param name="workflowInstance">Workflow instance.</param>
        internal WorkflowTaskEventArgs(WorkflowInstance workflowInstance) =>
            this.workflowInstance = workflowInstance;

        /// <summary>
        /// Gets the workflow information.
        /// </summary>
        public Workflows.WorkflowInfo WorkflowInfo => workflowInstance.Info;

        /// <summary>
        /// Gets the workflow definition.
        /// </summary>
        public Workflows.WorkflowDefinition WorkflowDefinition => workflowInstance.Definition;
    }
}