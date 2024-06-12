using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Runtime;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Options for a <see cref="WorkflowReplayer" />. At least one workflow is required.
    /// </summary>
    public class WorkflowReplayerOptions : ICloneable
    {
        private IList<WorkflowDefinition> workflows = new List<WorkflowDefinition>();

        /// <summary>
        /// Event for when a workflow task is starting. This should only be used for very advanced
        /// scenarios.
        /// </summary>
        /// <remarks>
        /// WARNING: This is experimental and there are many caveats about its use. It is important
        /// to read the documentation on <see cref="TemporalWorkerOptions.WorkflowTaskStarting" />.
        /// </remarks>
        public event EventHandler<WorkflowTaskStartingEventArgs>? WorkflowTaskStarting;

        /// <summary>
        /// Event for when a workflow task has completed but not yet sent back to the server. This
        /// should only be used for very advanced scenarios.
        /// </summary>
        /// <remarks>
        /// WARNING: This is experimental and there are many caveats about its use. It is important
        /// to read the documentation on <see cref="TemporalWorkerOptions.WorkflowTaskStarting" />.
        /// </remarks>
        public event EventHandler<WorkflowTaskCompletedEventArgs>? WorkflowTaskCompleted;

        /// <summary>
        /// Gets the workflow definitions.
        /// </summary>
        public IList<WorkflowDefinition> Workflows => workflows;

        /// <summary>
        /// Gets or sets the namespace. Default is "ReplayNamespace".
        /// </summary>
        public string Namespace { get; set; } = "ReplayNamespace";

        /// <summary>
        /// Gets or sets the task queue. Default is "ReplayTaskQueue". This is often only used for
        /// logging so this does not need to be set for most uses of a replayer.
        /// </summary>
        public string TaskQueue { get; set; } = "ReplayTaskQueue";

        /// <summary>
        /// Gets or sets the data converter. Default is
        /// <see cref="Converters.DataConverter.Default" />.
        /// </summary>
        public Converters.DataConverter DataConverter { get; set; } =
            Converters.DataConverter.Default;

        /// <summary>
        /// Gets or sets the interceptors.
        /// </summary>
        public IReadOnlyCollection<Interceptors.IWorkerInterceptor>? Interceptors { get; set; }

        /// <summary>
        /// Gets or sets the build ID. This is a unique identifier for each "build" of the worker.
        /// If unset, the default is
        /// <c>Assembly.GetEntryAssembly().ManifestModule.ModuleVersionId</c>.
        /// </summary>
        public string? BuildId { get; set; }

        /// <summary>
        /// Gets or sets the identity for this worker.
        /// </summary>
        public string? Identity { get; set; }

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
            string.Equals(TemporalWorkerOptions.DebugModeEnvironmentVariable, "true", StringComparison.OrdinalIgnoreCase) ||
            TemporalWorkerOptions.DebugModeEnvironmentVariable == "1";

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
        public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        /// <summary>
        /// Gets or sets runtime for this replayer.
        /// </summary>
        /// <remarks>
        /// By default this uses <see cref="TemporalRuntime.Default" /> which is lazily created when
        /// first needed.
        /// </remarks>
        public TemporalRuntime? Runtime { get; set; }

        /// <summary>
        /// Gets or sets workflow failure exception types. See
        /// <see cref="TemporalWorkerOptions.WorkflowFailureExceptionTypes" />.
        /// </summary>
        public IReadOnlyCollection<Type>? WorkflowFailureExceptionTypes { get; set; }

        /// <summary>
        /// Gets or sets a function to create workflow instances.
        /// </summary>
        /// <remarks>
        /// Don't expose this until there's a use case.
        /// </remarks>
        internal Func<WorkflowInstanceDetails, IWorkflowInstance> WorkflowInstanceFactory { get; set; } =
            TemporalWorkerOptions.DefaultWorkflowInstanceFactory;

        /// <summary>
        /// Gets or sets a value indicating whether the workflow completion command reordering will
        /// apply.
        /// </summary>
        /// <remarks>
        /// This is visible for testing only.
        /// </remarks>
        internal bool DisableWorkflowCompletionCommandReordering { get; set; }

        /// <summary>
        /// Add the given type as a workflow.
        /// </summary>
        /// <typeparam name="T">Type to add.</typeparam>
        /// <returns>This options instance for chaining.</returns>
        public WorkflowReplayerOptions AddWorkflow<T>() => AddWorkflow(typeof(T));

        /// <summary>
        /// Add the given type as a workflow.
        /// </summary>
        /// <param name="type">Type to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public WorkflowReplayerOptions AddWorkflow(Type type) =>
            AddWorkflow(WorkflowDefinition.Create(type));

        /// <summary>
        /// Add the given workflow definition. Most users will use <see cref="AddWorkflow{T}" />
        /// instead.
        /// </summary>
        /// <param name="definition">Definition to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public WorkflowReplayerOptions AddWorkflow(WorkflowDefinition definition)
        {
            Workflows.Add(definition);
            return this;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.
        /// Also copies collections of workflows.</returns>
        public virtual object Clone()
        {
            var options = (WorkflowReplayerOptions)MemberwiseClone();
            options.workflows = new List<WorkflowDefinition>(Workflows);
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