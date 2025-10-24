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
    public class SimplePluginOptions
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
        public SimplePluginRequiredOption<Converters.DataConverter>? DataConverterOption
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
        public SimplePluginOption<IReadOnlyCollection<IClientInterceptor>>? ClientInterceptorsOption
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the activities for the plugin.
        /// </summary>
        public IList<ActivityDefinition>? Activities
        {
            get;
            init;
        }

        /// <summary>
        /// Gets the workflows for the plugin.
        /// </summary>
        public IList<WorkflowDefinition>? Workflows
        {
            get;
            init;
        }

        /// <summary>
        /// Gets the nexus services for the plugin.
        /// </summary>
        public IList<ServiceHandlerInstance>? NexusServices
        {
            get;
            init;
        }

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
        public SimplePluginOption<IReadOnlyCollection<IWorkerInterceptor>>? WorkerInterceptorsOption
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
        public SimplePluginOption<IReadOnlyCollection<Type>>? WorkflowFailureExceptionTypesOption
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the run context function for the plugin.
        /// </summary>
        public Func<Func<Task<object?>>, Task<object?>>? RunContext
        {
            get;
            set;
        }

        /// <summary>
        /// Represents a configurable plugin option.
        /// </summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        public class SimplePluginOption<T>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SimplePluginOption{T}"/> class with a constant value.
            /// </summary>
            /// <param name="value">The constant value.</param>
            public SimplePluginOption(T? value)
            {
                Constant = value;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="SimplePluginOption{T}"/> class with a configurable function.
            /// </summary>
            /// <param name="value">The configurable function.</param>
            public SimplePluginOption(Func<T?, T> value)
            {
                Configurable = value;
            }

            /// <summary>
            /// Gets the configurable function for the option.
            /// </summary>
            public Func<T?, T>? Configurable { get; }

            /// <summary>
            /// Gets the constant value for the option.
            /// </summary>
            public T? Constant { get; }
        }

        /// <summary>
        /// Represents a required configurable plugin option.
        /// </summary>
        /// <typeparam name="T">The type of the option value.</typeparam>
        public class SimplePluginRequiredOption<T>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SimplePluginRequiredOption{T}"/> class with a constant value.
            /// </summary>
            /// <param name="value">The constant value.</param>
            public SimplePluginRequiredOption(T? value)
            {
                Constant = value;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="SimplePluginRequiredOption{T}"/> class with a configurable function.
            /// </summary>
            /// <param name="value">The configurable function.</param>
            public SimplePluginRequiredOption(Func<T, T> value)
            {
                Configurable = value;
            }

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