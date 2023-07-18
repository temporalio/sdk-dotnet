using System;
using System.Collections.Generic;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for starting a workflow. <see cref="Id" /> and <see cref="TaskQueue" /> are
    /// required.
    /// </summary>
    public class WorkflowOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowOptions"/> class.
        /// </summary>
        public WorkflowOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowOptions"/> class.
        /// </summary>
        /// <param name="id">Workflow ID.</param>
        /// <param name="taskQueue">Task queue to start workflow on.</param>
        public WorkflowOptions(string id, string taskQueue)
        {
            Id = id;
            TaskQueue = taskQueue;
        }

        /// <summary>
        /// Gets or sets the unique workflow identifier. This is required.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the task queue to run the workflow on. This is required.
        /// </summary>
        public string? TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets the total workflow execution timeout including retries and continue as new.
        /// </summary>
        public TimeSpan? ExecutionTimeout { get; set; }

        /// <summary>
        /// Gets or sets the timeout of a single workflow run.
        /// </summary>
        public TimeSpan? RunTimeout { get; set; }

        /// <summary>
        /// Gets or sets the timeout of a single workflow task.
        /// </summary>
        public TimeSpan? TaskTimeout { get; set; }

        /// <summary>
        /// Gets or sets how already-existing IDs are treated. Default is
        /// <see cref="WorkflowIdReusePolicy.AllowDuplicate" />.
        /// </summary>
        public WorkflowIdReusePolicy IdReusePolicy { get; set; } = WorkflowIdReusePolicy.AllowDuplicate;

        /// <summary>
        /// Gets or sets the retry policy for the workflow. If unset, workflow never retries.
        /// </summary>
        public RetryPolicy? RetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets the cron schedule for the workflow.
        /// </summary>
        public string? CronSchedule { get; set; }

        /// <summary>
        /// Gets or sets the memo for the workflow. Values for the memo cannot be null.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Memo { get; set; }

        /// <summary>
        /// Gets or sets the search attributes for the workflow.
        /// </summary>
        public SearchAttributeCollection? TypedSearchAttributes { get; set; }

        /// <summary>
        /// Gets or sets the start signal for the workflow. If this is non-null, a signal-with-start
        /// is used instead of a traditional workflow start. This means the workflow will only be
        /// created if it does not already exist, then a signal will be sent.
        /// </summary>
        public string? StartSignal { get; set; }

        /// <summary>
        /// Gets or sets the arguments for the start signal. This cannot be set if
        /// <see cref="StartSignal" /> is not set.
        /// </summary>
        public IReadOnlyCollection<object?>? StartSignalArgs { get; set; }

        /// <summary>
        /// Gets or sets RPC options for starting the workflow.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (WorkflowOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}