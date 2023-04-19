using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Runtime;

namespace Temporalio.Worker
{
    /// <summary>
    /// Options for a <see cref="WorkflowReplayer" />. At least one workflow is required.
    /// </summary>
    public class WorkflowReplayerOptions : ICloneable
    {
#pragma warning disable CA2227 // Intentionally allow setting of this collection w/ options pattern
        /// <summary>
        /// Gets or sets the workflow types.
        /// </summary>
        public IList<Type> Workflows { get; set; } = new List<Type>();

        /// <summary>
        /// Gets or sets additional workflow definitions.
        /// </summary>
        /// <remarks>
        /// Unless manual definitions are required, users should use <see cref="Workflows" />.
        /// </remarks>
        public IList<Workflows.WorkflowDefinition> AdditionalWorkflowDefinitions { get; set; } = new List<Workflows.WorkflowDefinition>();
#pragma warning restore CA2227

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
        public IEnumerable<Interceptors.IWorkerInterceptor>? Interceptors { get; set; }

        /// <summary>
        /// Gets or sets the build ID. This is a unique identifier for each "build" of the worker.
        /// If unset, the default is
        /// <c>Assembly.GetEntryAssembly().ManifestModule.ModuleVersionId</c>.
        /// </summary>
        public string? BuildID { get; set; }

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
        /// Gets or sets a function to create workflow instances.
        /// </summary>
        /// <remarks>
        /// Don't expose this until there's a use case.
        /// </remarks>
        internal Func<WorkflowInstanceDetails, IWorkflowInstance>? WorkflowInstanceFactory { get; set; }

        /// <summary>
        /// Add the given type as a workflow.
        /// </summary>
        /// <param name="type">Type to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public WorkflowReplayerOptions AddWorkflow(Type type)
        {
            Workflows.Add(type);
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
            options.Workflows = new List<Type>(Workflows);
            options.AdditionalWorkflowDefinitions =
                new List<Workflows.WorkflowDefinition>(AdditionalWorkflowDefinitions);
            return options;
        }
    }
}