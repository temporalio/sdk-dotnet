using System;
using System.Collections.Generic;
using System.Threading;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for starting a child workflow.
    /// </summary>
    public class ChildWorkflowOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the workflow ID. If unset, default will be a deterministic-random
        /// identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the task queue. If unset, default will be the parent workflow task queue.
        /// </summary>
        public string? TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets a single-line fixed summary for this workflow execution that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticSummary { get; set; }

        /// <summary>
        /// Gets or sets general fixed details for this workflow execution that may appear in
        /// UI/CLI. This can be in Temporal markdown format and can span multiple lines. This is a
        /// fixed value on the workflow that cannot be updated. For details that can be updated, use
        /// <see cref="Workflow.CurrentDetails" /> within the workflow.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticDetails { get; set; }

        /// <summary>
        /// Gets or sets the retry policy. Default is no retries.
        /// </summary>
        public RetryPolicy? RetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets how the workflow will send/wait for cancellation of the child. Default is
        /// <see cref="ChildWorkflowCancellationType.WaitCancellationCompleted" />.
        /// </summary>
        public ChildWorkflowCancellationType CancellationType { get; set; } = ChildWorkflowCancellationType.WaitCancellationCompleted;

        /// <summary>
        /// Gets or sets the cancellation token for this child. If unset, defaults to the workflow
        /// cancellation token.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets how the child is treated when the parent is closed. Default is
        /// <see cref="ParentClosePolicy.Terminate" />.
        /// </summary>
        public ParentClosePolicy ParentClosePolicy { get; set; } = ParentClosePolicy.Terminate;

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
        /// Gets or sets whether to allow re-using a workflow ID from a previously *closed* workflow.
        /// Default is <see cref="WorkflowIdReusePolicy.AllowDuplicate" />.
        /// </summary>
        public WorkflowIdReusePolicy IdReusePolicy { get; set; } = WorkflowIdReusePolicy.AllowDuplicate;

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
        /// Gets or sets whether this Child Workflow should run on a worker with a compatible Build Id or not when using
        /// the Worker Versioning feature.
        /// </summary>
        public VersioningIntent VersioningIntent { get; set; } = VersioningIntent.Unspecified;

        /// <summary>
        /// Gets or sets the priority that should be used for this Child Workflow.
        /// </summary>
        public Priority? Priority { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
