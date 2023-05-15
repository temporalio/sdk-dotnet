using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Options for a <see cref="TemporalWorker" />. <see cref="TaskQueue" /> and at least one
    /// workflow or activity are required. Most users will use <see cref="AddActivity(Delegate)" />
    /// and/or <see cref="AddWorkflow{T}" /> to add activities and workflows.
    /// </summary>
    public class TemporalWorkerOptions : ICloneable
    {
        private IList<ActivityDefinition> activities = new List<ActivityDefinition>();
        private IList<WorkflowDefinition> workflows = new List<WorkflowDefinition>();

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
        /// Gets the activity definitions. Most users will use AddActivity to add to this list.
        /// </summary>
        public IList<ActivityDefinition> Activities => activities;

        /// <summary>
        /// Gets the workflow definitions. Most users will use AddWorkflow to add to this list.
        /// </summary>
        public IList<WorkflowDefinition> Workflows => workflows;

        /// <summary>
        /// Gets or sets the task queue for activities. Default is <see cref="Task.Factory" />.
        /// </summary>
        public TaskFactory ActivityTaskFactory { get; set; } = Task.Factory;

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
        /// Gets or sets a value indicating whether workflow tracing event listener will be disabled
        /// for all workflows.
        /// </summary>
        /// <remarks>
        /// When false, the default, a <see cref="System.Diagnostics.Tracing.EventListener" /> is
        /// used to catch improper calls from inside the workflow.
        /// </remarks>
        public bool DisableWorkflowTracingEventListener { get; set; }

        /// <summary>
        /// Gets or sets the logging factory used by loggers in workers. If unset, defaults to the
        /// client logger factory.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>
        /// Gets or sets the form of workflow stack trace queries are supported. Default is "None"
        /// which means workflow stack trace are not supported and will fail.
        /// </summary>
        /// <remarks>
        /// Currently due to internal implementation details, stack traces have to be captured
        /// eagerly on every Temporal task creation that can be waited on. Due to this performance
        /// cost, they are turned off by default.
        /// </remarks>
        public WorkflowStackTrace WorkflowStackTrace { get; set; } = WorkflowStackTrace.None;

        /// <summary>
        /// Gets the TEMPORAL_DEBUG environment variable.
        /// </summary>
        internal static string? DebugModeEnvironmentVariable { get; } = Environment.GetEnvironmentVariable("TEMPORAL_DEBUG");

        /// <summary>
        /// Gets or sets a function to create workflow instances.
        /// </summary>
        /// <remarks>
        /// Don't expose this until there's a use case.
        /// </remarks>
        internal Func<WorkflowInstanceDetails, IWorkflowInstance>? WorkflowInstanceFactory { get; set; }

        /// <summary>
        /// Add the given delegate with <see cref="ActivityAttribute" /> as an activity. This is
        /// usually a method reference.
        /// </summary>
        /// <param name="del">Delegate to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddActivity(Delegate del) =>
            AddActivity(ActivityDefinition.Create(del));

        /// <summary>
        /// Add the given activity definition. Most users will use
        /// <see cref="AddActivity(Delegate)" /> instead.
        /// </summary>
        /// <param name="definition">Definition to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddActivity(ActivityDefinition definition)
        {
            Activities.Add(definition);
            return this;
        }

        /// <summary>
        /// Add all methods on the given type with <see cref="ActivityAttribute" />.
        /// </summary>
        /// <typeparam name="T">Type to get activities from.</typeparam>
        /// <param name="instance">Instance to use when invoking. This must be non-null if any
        /// activities are non-static.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddAllActivities<T>(T? instance) =>
            AddAllActivities(typeof(T), instance);

        /// <summary>
        /// Add all methods on the given type with <see cref="ActivityAttribute" />.
        /// </summary>
        /// <param name="type">Type to get activities from.</param>
        /// <param name="instance">Instance to use when invoking. This must be non-null if any
        /// activities are non-static.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddAllActivities(Type type, object? instance)
        {
            foreach (var defn in ActivityDefinition.CreateAll(type, instance))
            {
                AddActivity(defn);
            }
            return this;
        }

        /// <summary>
        /// Add the given type as a workflow.
        /// </summary>
        /// <typeparam name="T">Type to add.</typeparam>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddWorkflow<T>() => AddWorkflow(typeof(T));

        /// <summary>
        /// Add the given type as a workflow.
        /// </summary>
        /// <param name="type">Type to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddWorkflow(Type type) =>
            AddWorkflow(WorkflowDefinition.Create(type));

        /// <summary>
        /// Add the given workflow definition. Most users will use <see cref="AddWorkflow{T}" />
        /// instead.
        /// </summary>
        /// <param name="definition">Definition to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public TemporalWorkerOptions AddWorkflow(WorkflowDefinition definition)
        {
            Workflows.Add(definition);
            return this;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.
        /// Also copies collections of activities and workflows.</returns>
        public virtual object Clone()
        {
            var options = (TemporalWorkerOptions)MemberwiseClone();
            options.activities = new List<ActivityDefinition>(Activities);
            options.workflows = new List<WorkflowDefinition>(Workflows);
            return options;
        }
    }
}