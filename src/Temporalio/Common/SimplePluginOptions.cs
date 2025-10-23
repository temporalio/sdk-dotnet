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
    public class SimplePluginOptions
    {
        public Converters.DataConverter? DataConverter
        {
            get => DataConverterOption?.Constant;
            set => DataConverterOption = new(value);
        }

        public SimplePluginRequiredOption<Converters.DataConverter>? DataConverterOption
        {
            get;
            set;
        }

        public IReadOnlyCollection<IClientInterceptor>? ClientInterceptors
        {
            get => ClientInterceptorsOption?.Constant;
            set => ClientInterceptorsOption = new(value);
        }

        public SimplePluginOption<IReadOnlyCollection<IClientInterceptor>>? ClientInterceptorsOption
        {
            get;
            set;
        }

        public IList<ActivityDefinition>? Activities
        {
            get;
            set;
        }

        public IList<WorkflowDefinition>? Workflows
        {
            get;
            set;
        }

        public IList<ServiceHandlerInstance>? NexusServices
        {
            get;
            set;
        }

        public IReadOnlyCollection<IWorkerInterceptor>? WorkerInterceptors
        {
            get => WorkerInterceptorsOption?.Constant;
            set => WorkerInterceptorsOption = new(value);
        }

        public SimplePluginOption<IReadOnlyCollection<IWorkerInterceptor>>? WorkerInterceptorsOption
        {
            get;
            set;
        }

        public IReadOnlyCollection<Type>? WorkflowFailureExceptionTypes
        {
            get => WorkflowFailureExceptionTypesOption?.Constant;
            set => WorkflowFailureExceptionTypesOption = new(value);
        }

        public SimplePluginOption<IReadOnlyCollection<Type>>? WorkflowFailureExceptionTypesOption
        {
            get;
            set;
        }

        public Func<Func<Task<object?>>, Task<object?>>? RunContext
        {
            get;
            set;
        }

        public class SimplePluginOption<T>
        {
            public Func<T?, T>? Configurable { get; }

            public T? Constant { get; }

            public SimplePluginOption(T? value)
            {
                Constant = value;
            }

            public SimplePluginOption(Func<T?, T> value)
            {
                Configurable = value;
            }
        }

        public class SimplePluginRequiredOption<T>
        {
            public Func<T, T>? Configurable { get; }

            public T? Constant { get; }

            public SimplePluginOption(T? value)
            {
                Constant = value;
            }

            public SimplePluginOption(Func<T, T> value)
            {
                Configurable = value;
            }
        }
    }
}