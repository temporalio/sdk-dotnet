using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
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
        /// Gets or sets a single-line fixed summary for this workflow execution that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticSummary { get; set; }

        /// <summary>
        /// Gets or sets general fixed details for this workflow execution that may appear in
        /// UI/CLI. This can be in Temporal markdown format and can span multiple lines. This is a
        /// fixed value on the workflow that cannot be updated. For details that can be updated, use
        /// <see cref="Workflows.Workflow.CurrentDetails" /> within the workflow.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticDetails { get; set; }

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
        /// Gets or sets how already-existing workflows of the same ID are treated. Default is
        /// <see cref="WorkflowIdConflictPolicy.Unspecified" /> which effectively means
        /// <see cref="WorkflowIdConflictPolicy.Fail"/> on the server. If this value is set, then
        /// <see cref="IdReusePolicy"/> cannot be set to
        /// <see cref="WorkflowIdReusePolicy.TerminateIfRunning"/>.
        /// </summary>
        public WorkflowIdConflictPolicy IdConflictPolicy { get; set; } = WorkflowIdConflictPolicy.Unspecified;

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
        /// Gets or sets the amount of time to wait before starting the workflow.
        /// </summary>
        /// <remarks>Start delay does not work with <see cref="CronSchedule" />.</remarks>
        public TimeSpan? StartDelay { get; set; }

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
        /// Gets or sets a value indicating whether the workflow will request eager start. This
        /// potentially reduces the latency to start the workflow by encouraging the server to
        /// start it on a local worker running this same client.
        /// </summary>
        public bool RequestEagerStart { get; set; }

        /// <summary>
        /// Gets or sets RPC options for starting the workflow.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Gets or sets the priority to use when starting this workflow.
        /// </summary>
        public Priority? Priority { get; set; }

        /// <summary>
        /// Gets or sets the versioning override to use when starting this workflow.
        /// </summary>
        public VersioningOverride? VersioningOverride { get; set; }

        /// <summary>
        /// Perform a signal-with-start which will only start the workflow if it's not already
        /// running, but send a signal to it regardless. This is just sugar for manually setting
        /// <see cref="StartSignal" /> and <see cref="StartSignalArgs" /> directly.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="signalCall">Invocation or a workflow signal method.</param>
        public void SignalWithStart<TWorkflow>(Expression<Func<TWorkflow, Task>> signalCall)
        {
            var (method, args) = ExpressionUtil.ExtractCall(signalCall);
            SignalWithStart(Workflows.WorkflowSignalDefinition.NameFromMethodForCall(method), args);
        }

        /// <summary>
        /// Perform a signal-with-start which will only start the workflow if it's not already
        /// running, but send a signal to it regardless. This is just sugar for manually setting
        /// <see cref="StartSignal" /> and <see cref="StartSignalArgs" /> directly.
        /// </summary>
        /// <param name="signal">Signal name.</param>
        /// <param name="args">Signal args.</param>
        public void SignalWithStart(string signal, IReadOnlyCollection<object?> args)
        {
            StartSignal = signal;
            StartSignalArgs = args;
        }

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
