using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Temporalio.Worker
{
    /// <summary>
    /// Options for a <see cref="TemporalWorker" />. <see cref="TaskQueue" /> and at least one
    /// workflow or activity are required.
    /// </summary>
    public class TemporalWorkerOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerOptions"/> class.
        /// </summary>
        public TemporalWorkerOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerOptions"/> class.
        /// </summary>
        /// <param name="taskQueue">Task queue for the worker.</param>
        public TemporalWorkerOptions(string taskQueue) => TaskQueue = taskQueue;

        /// <summary>
        /// Gets or sets the task queue for the worker.
        /// </summary>
        public string? TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets the task queue for activities. Default is <see cref="Task.Factory" />.
        /// </summary>
        public TaskFactory ActivityTaskFactory { get; set; } = Task.Factory;

#pragma warning disable CA2227 // Intentionally allow setting of this collection w/ options pattern
        /// <summary>
        /// Gets or sets the activity delegates.
        /// </summary>
        public IList<Delegate> Activities { get; set; } = new List<Delegate>();

        /// <summary>
        /// Gets or sets the workflow types.
        /// </summary>
        public IList<Type> Workflows { get; set; } = new List<Type>();

        /// <summary>
        /// Gets or sets additional activity definitions.
        /// </summary>
        /// <remarks>
        /// Unless manual definitions are required, users should use <see cref="Activities" />.
        /// </remarks>
        public IList<Activities.ActivityDefinition> AdditionalActivityDefinitions { get; set; } = new List<Activities.ActivityDefinition>();

        /// <summary>
        /// Gets or sets additional workflow definitions.
        /// </summary>
        /// <remarks>
        /// Unless manual definitions are required, users should use <see cref="Workflows" />.
        /// </remarks>
        public IList<Workflows.WorkflowDefinition> AdditionalWorkflowDefinitions { get; set; } = new List<Workflows.WorkflowDefinition>();
#pragma warning restore CA2227

        /// <summary>
        /// Gets or sets the interceptors. Note this automatically includes any
        /// <see cref="Client.TemporalClientOptions.Interceptors" /> that implement
        /// <see cref="Interceptors.IWorkerInterceptor" /> so those should not be specified here.
        /// This set is chained after the set in the client options.
        /// </summary>
        public IEnumerable<Interceptors.IWorkerInterceptor>? Interceptors { get; set; }

        /// <summary>
        /// Gets or sets the build ID. This is a unique identifier for each "build" of the worker.
        /// If unset, the default is
        /// <c>Assembly.GetEntryAssembly().ManifestModule.ModuleVersionId</c>.
        /// </summary>
        public string? BuildID { get; set; }

        /// <summary>
        /// Gets or sets the identity for this worker. If unset, defaults to the client's identity.
        /// </summary>
        public string? Identity { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activities that will ever be given to this worker
        /// concurrently. Default is 100.
        /// </summary>
        public int MaxConcurrentActivities { get; set; } = 100;

        /// <summary>
        /// Gets or sets the longest interval for throttling activity heartbeats. Default is 60s.
        /// </summary>
        public TimeSpan MaxHeartbeatThrottleInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the default interval for throttling activity heartbeats in case
        /// per-activity heartbeat timeout is unset. Otherwise, it's the per-activity heartbeat
        /// timeout * 0.8. Default is 30s.
        /// </summary>
        public TimeSpan DefaultHeartbeatThrottleInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the limit for the number of activities per second that this worker will
        /// process. The worker will not poll for new activities if by doing so it might receive and
        /// execute an activity which would cause it to exceed this limit.
        /// </summary>
        public double? MaxActivitiesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activities per second the task queue will dispatch,
        /// controlled server-side. Note that this only takes effect upon an activity poll request.
        /// If multiple workers on the same queue have different values set, they will thrash with
        /// the last poller winning.
        /// </summary>
        public double? MaxTaskQueueActivitiesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the amount of time after shutdown is called that activities are given to
        /// complete before they are cancelled.
        /// </summary>
        public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Gets or sets the number of workflows cached for sticky task queue use. If this is 0,
        /// sticky task queues are disabled and no caching occurs. Default is 10000.
        /// </summary>
        public int MaxCachedWorkflows { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the maximum allowed number of workflow tasks that will ever be given to
        /// the worker at one time. Default is 100.
        /// </summary>
        public int MaxConcurrentWorkflowTasks { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum number of local activities that will ever be given to this
        /// worker concurrently. Default is 100.
        /// </summary>
        public int MaxConcurrentLocalActivities { get; set; } = 100;

        /// <summary>
        /// Gets or sets a value indicating whether the worker will only handle workflows and local
        /// activities.
        /// </summary>
        public bool LocalActivityWorkerOnly { get; set; }

        /// <summary>
        /// Gets or sets how long a workflow task is allowed to sit on the sticky queue before it is
        /// timed out and moved to the non-sticky queue where it may be picked up by any worker.
        /// Default is 10s.
        /// </summary>
        public TimeSpan StickyQueueScheduleToStartTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets a value indicating whether deadlock detection will be disabled for all
        /// workflows. If unset, this value defaults to true only if the <c>TEMPORAL_DEBUG</c>
        /// environment variable is <c>true</c> or <c>1</c>.
        /// </summary>
        /// <remarks>
        /// When false, the default, deadlock detection prevents workflow tasks from taking too long
        /// before yielding back to Temporal. This is undesirable when stepping through code, so
        /// this should be set to true in those cases.
        /// </remarks>
        public bool DebugMode { get; set; } =
            string.Equals(DebugModeEnvironmentVariable, "true", StringComparison.OrdinalIgnoreCase) ||
            DebugModeEnvironmentVariable == "1";

        /// <summary>
        /// Gets or sets a value indicating whether workflow task tracing will be disabled for all
        /// workflows.
        /// </summary>
        /// <remarks>
        /// When false, the default, a <see cref="System.Diagnostics.Tracing.EventListener" /> is
        /// used to catch improper use of tasks outside of the built-in task scheduler.
        /// </remarks>
        public bool DisableWorkflowTaskTracing { get; set; }

        /// <summary>
        /// Gets or sets the logging factory used by loggers in workers. If unset, defaults to the
        /// client logger factory.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// Gets or sets a function to create workflow instances.
        /// </summary>
        /// <remarks>
        /// Don't expose this until there's a use case.
        /// </remarks>
        internal Func<WorkflowInstanceDetails, IWorkflowInstance>? WorkflowInstanceFactory { get; set; }

        private static string? DebugModeEnvironmentVariable { get; } = Environment.GetEnvironmentVariable("TEMPORAL_DEBUG");

        /// <summary>
        /// Add the given delegate as an activity.
        /// </summary>
        /// <param name="del">Delegate to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddActivity(Delegate del)
        {
            Activities.Add(del);
            return this;
        }

        /// <summary>
        /// Add the given type as a workflow.
        /// </summary>
        /// <param name="type">Type to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddWorkflow(Type type)
        {
            Workflows.Add(type);
            return this;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}