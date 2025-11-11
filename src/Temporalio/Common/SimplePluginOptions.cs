using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Activities;
using Temporalio.Client.Interceptors;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

namespace Temporalio.Common
{
    /// <summary>
    /// Configuration options for simple plugins.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public class SimplePluginOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the data converter for the plugin.
        /// </summary>
        public Converters.DataConverter? DataConverter
        {
            get => DataConverterOption?.Constant;
            set => DataConverterOption = new(value);
        }

        /// <summary>
        /// Gets or sets the data converter option with configuration support.
        /// </summary>
        public SimplePluginOption<Converters.DataConverter>? DataConverterOption
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the client interceptors for the plugin.
        /// </summary>
        public IReadOnlyCollection<IClientInterceptor>? ClientInterceptors
        {
            get => ClientInterceptorsOption?.Constant;
            set => ClientInterceptorsOption = new(value);
        }

        /// <summary>
        /// Gets or sets the client interceptors option with configuration support.
        /// </summary>
        public SimplePluginOption<IReadOnlyCollection<IClientInterceptor>?>? ClientInterceptorsOption
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the activity definitions. Most users will use AddActivity to add to this list.
        /// </summary>
        public IList<ActivityDefinition> Activities { get; } = new List<ActivityDefinition>();

        /// <summary>
        /// Gets the workflow definitions. Most users will use AddWorkflow to add to this list.
        /// </summary>
        public IList<WorkflowDefinition> Workflows { get; } = new List<WorkflowDefinition>();

        /// <summary>
        /// Gets the Nexus service instances. Most users will use AddNexusService to add to this
        /// list.
        /// </summary>
        /// <remarks>WARNING: Nexus support is experimental.</remarks>
        public IList<ServiceHandlerInstance> NexusServices { get; } = new List<ServiceHandlerInstance>();

        /// <summary>
        /// Gets or sets the worker interceptors for the plugin.
        /// </summary>
        public IReadOnlyCollection<IWorkerInterceptor>? WorkerInterceptors
        {
            get => WorkerInterceptorsOption?.Constant;
            set => WorkerInterceptorsOption = new(value);
        }

        /// <summary>
        /// Gets or sets the worker interceptors option with configuration support.
        /// </summary>
        public SimplePluginOption<IReadOnlyCollection<IWorkerInterceptor>?>? WorkerInterceptorsOption
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the workflow failure exception types for the plugin.
        /// </summary>
        public IReadOnlyCollection<Type>? WorkflowFailureExceptionTypes
        {
            get => WorkflowFailureExceptionTypesOption?.Constant;
            set => WorkflowFailureExceptionTypesOption = new(value);
        }

        /// <summary>
        /// Gets or sets the workflow failure exception types option with configuration support.
        /// </summary>
        public SimplePluginOption<IReadOnlyCollection<Type>?>? WorkflowFailureExceptionTypesOption
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a function to run before running worker/replayer.
        /// </summary>
        public Func<Task>? RunContextBefore { get; set; }

        /// <summary>
        /// Gets or sets a function to run after running worker/replayer.
        /// </summary>
        public Func<Task>? RunContextAfter { get; set; }

        /// <summary>
        /// Add the given delegate with <see cref="ActivityAttribute" /> as an activity. This is
        /// usually a method reference.
        /// </summary>
        /// <param name="del">Delegate to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public SimplePluginOptions AddActivity(Delegate del) =>
            AddActivity(ActivityDefinition.Create(del));

        /// <summary>
        /// Add the given activity definition. Most users will use
        /// <see cref="AddActivity(Delegate)" /> instead.
        /// </summary>
        /// <param name="definition">Definition to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public SimplePluginOptions AddActivity(ActivityDefinition definition)
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
        public SimplePluginOptions AddAllActivities<T>(T? instance) =>
            AddAllActivities(typeof(T), instance);

        /// <summary>
        /// Add all methods on the given type with <see cref="ActivityAttribute" />.
        /// </summary>
        /// <param name="type">Type to get activities from.</param>
        /// <param name="instance">Instance to use when invoking. This must be non-null if any
        /// activities are non-static.</param>
        /// <returns>This options instance for chaining.</returns>
        public SimplePluginOptions AddAllActivities(Type type, object? instance)
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
        public SimplePluginOptions AddWorkflow<T>() => AddWorkflow(typeof(T));

        /// <summary>
        /// Add the given type as a workflow.
        /// </summary>
        /// <param name="type">Type to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public SimplePluginOptions AddWorkflow(Type type) =>
            AddWorkflow(WorkflowDefinition.Create(type));

        /// <summary>
        /// Add the given workflow definition. Most users will use <see cref="AddWorkflow{T}" />
        /// instead.
        /// </summary>
        /// <param name="definition">Definition to add.</param>
        /// <returns>This options instance for chaining.</returns>
        public SimplePluginOptions AddWorkflow(WorkflowDefinition definition)
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
        public SimplePluginOptions AddNexusService(object serviceHandler)
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
            return (SimplePluginOptions)MemberwiseClone();
        }

        /// <summary>
        /// Represents a required configurable plugin option.
        /// </summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        public class SimplePluginOption<T>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SimplePluginOption{T}"/> class with a constant value.
            /// </summary>
            /// <param name="value">The constant value.</param>
            public SimplePluginOption(T? value) => Constant = value;

            /// <summary>
            /// Initializes a new instance of the <see cref="SimplePluginOption{T}"/> class with a configurable function.
            /// </summary>
            /// <param name="value">The configurable function.</param>
            public SimplePluginOption(Func<T, T> value) => Configurable = value;

            /// <summary>
            /// Gets the configurable function for the required option.
            /// </summary>
            public Func<T, T>? Configurable { get; }

            /// <summary>
            /// Gets the constant value for the required option.
            /// </summary>
            public T? Constant { get; }
        }
    }
}