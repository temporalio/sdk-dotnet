using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Worker.Tuning;
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
        /// <summary>
        /// Default workflow instance factory.
        /// </summary>
        internal static readonly Func<WorkflowInstanceDetails, IWorkflowInstance> DefaultWorkflowInstanceFactory =
            details => new WorkflowInstance(details);

        private IList<ActivityDefinition> activities = new List<ActivityDefinition>();
        private IList<WorkflowDefinition> workflows = new List<WorkflowDefinition>();
        private IList<ServiceHandlerInstance> nexusServices = new List<ServiceHandlerInstance>();

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
        /// Event for when a workflow task is starting. This should only be used for very advanced
        /// scenarios.
        /// </summary>
        /// <remarks>
        /// WARNING: This is experimental and may change in the future.
        /// </remarks>
        /// <remarks>
        /// WARNING: As currently implemented, this does not currently represent workflow tasks as
        /// Temporal server defines them. Rather this is SDK "activations" of which there may be
        /// multiple in a single task if a user, for example, uses local activities. This may change
        /// in the future.
        /// </remarks>
        /// <remarks>
        /// WARNING: If a task fails (not a workflow failure, but a non-Temporal exception from a
        /// task causing task failure), the task will continually retry causing new task events.
        /// </remarks>
        /// <remarks>
        /// WARNING: In the case of a deadlock (i.e. task taking longer than 2 seconds), a
        /// <see cref="WorkflowTaskCompleted" /> event may not occur.
        /// </remarks>
        /// <remarks>
        /// WARNING: Adding/removing to/from this event or <see cref="WorkflowTaskCompleted" /> must
        /// be done before constructing the worker. Since the worker clones these options on
        /// construction, any alterations after construction will not apply.
        /// </remarks>
        public event EventHandler<WorkflowTaskStartingEventArgs>? WorkflowTaskStarting;

        /// <summary>
        /// Event for when a workflow task has completed but not yet sent back to the server. This
        /// should only be used for very advanced scenarios.
        /// </summary>
        /// <remarks>
        /// WARNING: This is experimental and there are many caveats about its use. It is important
        /// to read the documentation on <see cref="WorkflowTaskStarting" />.
        /// </remarks>
        public event EventHandler<WorkflowTaskCompletedEventArgs>? WorkflowTaskCompleted;

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
        /// Gets the Nexus service instances. Most users will use AddNexusService to add to this
        /// list.
        /// </summary>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
        public IList<ServiceHandlerInstance> NexusServices => nexusServices;

        /// <summary>
        /// Gets or sets the task factory for activities. Default is <see cref="Task.Factory" />.
        /// </summary>
        public TaskFactory ActivityTaskFactory { get; set; } = Task.Factory;

        /// <summary>
        /// Gets or sets the task factory for Nexus tasks. Default is <see cref="Task.Factory" />.
        /// </summary>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
        public TaskFactory NexusTaskFactory { get; set; } = Task.Factory;

        /// <summary>
        /// Gets or sets the interceptors. Note this automatically includes any
        /// <see cref="Client.TemporalClientOptions.Interceptors" /> that implement
        /// <see cref="Interceptors.IWorkerInterceptor" /> so those should not be specified here.
        /// This set is chained after the set in the client options.
        /// </summary>
        public IReadOnlyCollection<Interceptors.IWorkerInterceptor>? Interceptors { get; set; }

        /// <summary>
        /// Gets or sets the build ID. This is a unique identifier for each "build" of the worker.
        /// If unset, the default is
        /// <c>Assembly.GetEntryAssembly().ManifestModule.ModuleVersionId</c>.
        /// </summary>
        /// <remarks>Exclusive with <see cref="DeploymentOptions"/>.</remarks>
        [Obsolete("Use DeploymentOptions instead")]
        public string? BuildId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this worker opts into the worker versioning feature. This ensures it
        /// only receives workflow tasks for workflows which it claims to be compatible with. The
        /// <see cref="BuildId"/> field is used as this worker's version when enabled, and must be set.
        /// </summary>
        /// <remarks>Exclusive with <see cref="DeploymentOptions"/>.</remarks>
        [Obsolete("Use DeploymentOptions instead")]
        public bool UseWorkerVersioning { get; set; }

        /// <summary>
        /// Gets or sets the deployment options for this worker.
        /// </summary>
        /// <remarks>Exclusive with <see cref="DeploymentOptions"/> and <see cref="UseWorkerVersioning"/>.</remarks>
        /// <remarks>WARNING: Deployment-based versioning is experimental and APIs may
        /// change.</remarks>
        public WorkerDeploymentOptions? DeploymentOptions { get; set; }

        /// <summary>
        /// Gets or sets the identity for this worker. If unset, defaults to the client's identity.
        /// </summary>
        public string? Identity { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activities that will ever be given to this worker
        /// concurrently. Default is 100. Mutually exclusive with <see cref="Tuner"/>.
        /// </summary>
        public int? MaxConcurrentActivities { get; set; }

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
        /// the worker at one time. Default is 100. Mutually exclusive with <see cref="Tuner"/>.
        /// </summary>
        public int? MaxConcurrentWorkflowTasks { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of local activities that will ever be given to this
        /// worker concurrently. Default is 100. Mutually exclusive with <see cref="Tuner"/>.
        /// </summary>
        public int? MaxConcurrentLocalActivities { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of Nexus tasks that will ever be given to this worker
        /// concurrently. Default is 100. Mutually exclusive with <see cref="Tuner"/>.
        /// </summary>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
        public int? MaxConcurrentNexusTasks { get; set; }

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
        /// Gets or sets the maximum number of concurrent poll workflow task requests we will
        /// perform at a time on this worker's task queue. Default is 5.
        /// </summary>
        public int MaxConcurrentWorkflowTaskPolls { get; set; } = 5;

        /// <summary>
        /// Gets or sets the sticky poll ratio. <see cref="MaxConcurrentWorkflowTaskPolls" /> times
        /// this value will be the number of max pollers that will be allowed for the non-sticky
        /// queue when sticky tasks are enabled. If both defaults are used, the sticky queue will
        /// allow 4 max pollers while the non-sticky queue will allow 1. The minimum for either
        /// poller is 1, so if <see cref="MaxConcurrentWorkflowTaskPolls" /> is 1 and sticky queues
        /// are enabled, there will be 2 concurrent polls. Default is 0.2.
        /// </summary>
        public float NonStickyToStickyPollRatio { get; set; } = 0.2F;

        /// <summary>
        /// Gets or sets the maximum number of concurrent poll activity task requests we will
        /// perform at a time on this worker's task queue. Default is 5.
        /// </summary>
        public int MaxConcurrentActivityTaskPolls { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum number of concurrent poll Nexus task requests we will perform
        /// at a time on this worker's task queue. Default is 5.
        /// </summary>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
        public int MaxConcurrentNexusTaskPolls { get; set; } = 5;

        /// <summary>
        /// Gets or sets the behavior of the workflow task poller.
        /// </summary>
        /// <remarks>If set, will override any value set in <see cref="MaxConcurrentWorkflowTaskPolls"/>.</remarks>
        /// <remarks>WARNING: This property is experimental.</remarks>
        public PollerBehavior? WorkflowTaskPollerBehavior { get; set; }

        /// <summary>
        /// Gets or sets the behavior of the activity task poller.
        /// </summary>
        /// <remarks>WARNING: This property is experimental.</remarks>
        /// <remarks>If set, will override any value set in <see cref="MaxConcurrentActivityTaskPolls"/>.</remarks>
        public PollerBehavior? ActivityTaskPollerBehavior { get; set; }

        /// <summary>
        /// Gets or sets the behavior of the Nexus task poller.
        /// </summary>
        /// <remarks>WARNING: This property is experimental.</remarks>
        /// <remarks>If set, will override any value set in <see cref="MaxConcurrentNexusTaskPolls"/>.</remarks>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
        public PollerBehavior? NexusTaskPollerBehavior { get; set; }

        /// <summary>
        /// Gets or sets the types of exceptions that, if a workflow-thrown exception extends, will
        /// cause the workflow/update to fail instead of suspending the workflow via task failure.
        /// These are applied in addition to <see cref="WorkflowAttribute.FailureExceptionTypes" />
        /// on a specific workflow. If <c>typeof(Exception)</c> is set, it effectively will fail a
        /// workflow/update in all user exception cases.
        /// </summary>
        /// <remarks>
        /// WARNING: This property is experimental and may change in the future. If unset
        /// (i.e. left null), currently the default is to only fail the workflow/update on
        /// <see cref="Exceptions.FailureException" /> + cancellation and suspend via task failure
        /// all others. But this default may change in the future.
        /// </remarks>
        public IReadOnlyCollection<Type>? WorkflowFailureExceptionTypes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether deadlock detection will be disabled for all
        /// workflows. If unset, this value defaults to true only if
        /// <see cref="Debugger.IsAttached" /> is <c>true</c> or the <c>TEMPORAL_DEBUG</c>
        /// environment variable is <c>true</c> or <c>1</c>.
        /// </summary>
        /// <remarks>
        /// When false, the default, deadlock detection prevents workflow tasks from taking too long
        /// before yielding back to Temporal. This is undesirable when stepping through code, so
        /// this should be set to true in those cases.
        /// </remarks>
        public bool DebugMode { get; set; } =
            string.Equals(DebugModeEnvironmentVariable, "true", StringComparison.OrdinalIgnoreCase) ||
            DebugModeEnvironmentVariable == "1" ||
            Debugger.IsAttached;

        /// <summary>
        /// Gets or sets a value indicating whether workflow tracing event listener will be disabled
        /// for all workflows.
        /// </summary>
        /// <remarks>
        /// When false, the default, a <see cref="System.Diagnostics.Tracing.EventListener" /> is
        /// used to catch improper calls from inside the workflow.
        /// </remarks>
        /// <seealso cref="Workflow.Unsafe.WithTracingEventListenerDisabled{T}(Func{T})"/>
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
        /// Gets or sets a custom <see cref="WorkerTuner"/>. Mutually exclusive with <see
        /// cref="MaxConcurrentActivities"/>, <see cref="MaxConcurrentWorkflowTasks"/> and <see
        /// cref="MaxConcurrentLocalActivities"/>.
        /// </summary>
        public WorkerTuner? Tuner { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether eager activity executions will be disabled from
        /// a workflow.
        /// </summary>
        /// <remarks>
        /// Eager activity execution is an optimization on some servers that sends activities back
        /// to the same worker as the calling workflow if they can run there.
        /// </remarks>
        /// <remarks>
        /// This should be set to <c>true</c> for <see cref="MaxTaskQueueActivitiesPerSecond" /> to
        /// work and in a future version of this API may be implied as such (i.e. this setting will
        /// be ignored if that setting is set).
        /// </remarks>
        public bool DisableEagerActivityExecution { get; set; }

        /// <summary>
        /// Gets or sets the plugins.
        /// </summary>
        public IReadOnlyCollection<ITemporalWorkerPlugin>? Plugins { get; set; }

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
        internal Func<WorkflowInstanceDetails, IWorkflowInstance> WorkflowInstanceFactory { get; set; } =
            DefaultWorkflowInstanceFactory;

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
        /// Add the given Nexus service handler.
        /// </summary>
        /// <param name="serviceHandler">Service handler to add. It is expected to be an instance of
        /// a class with a <see cref="NexusServiceHandlerAttribute"/> attribute.</param>
        /// <returns>This options instance for chaining.</returns>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
        public TemporalWorkerOptions AddNexusService(object serviceHandler)
        {
            NexusServices.Add(ServiceHandlerInstance.FromInstance(serviceHandler));
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
            options.nexusServices = new List<ServiceHandlerInstance>(NexusServices);
            if (options.DeploymentOptions != null)
            {
                options.DeploymentOptions = (WorkerDeploymentOptions)options.DeploymentOptions.Clone();
            }
            return options;
        }

        /// <summary>
        /// Callback for task starting.
        /// </summary>
        /// <param name="instance">Workflow instance.</param>
        internal void OnTaskStarting(WorkflowInstance instance)
        {
            if (WorkflowTaskStarting is { } handler)
            {
                handler(instance, new(instance));
            }
        }

        /// <summary>
        /// Callback for task completed.
        /// </summary>
        /// <param name="instance">Workflow instance.</param>
        /// <param name="failureException">Task failure exception.</param>
        internal void OnTaskCompleted(WorkflowInstance instance, Exception? failureException)
        {
            if (WorkflowTaskCompleted is { } handler)
            {
                handler(instance, new(instance, failureException));
            }
        }
    }
}
