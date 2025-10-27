using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Worker;

namespace Temporalio.Common
{
    /// <summary>
    /// A simple plugin that implements both client and worker plugin interfaces.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public class SimplePlugin : ITemporalClientPlugin, ITemporalWorkerPlugin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimplePlugin"/> class.
        /// </summary>
        /// <param name="name">The plugin name.</param>
        /// <param name="options">The plugin options.</param>
        public SimplePlugin(
            string name,
            SimplePluginOptions? options = null)
        {
            Name = name;
            PluginOptions = options ?? new SimplePluginOptions();
        }

        /// <summary>
        /// Gets the plugin options.
        /// </summary>
        public SimplePluginOptions PluginOptions { get; init; }

        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Configures the client options.
        /// </summary>
        /// <param name="options">The client options to configure.</param>
        public virtual void ConfigureClient(TemporalClientOptions options)
        {
            options.DataConverter = Resolve(options.DataConverter, PluginOptions.DataConverterOption);
            options.Interceptors = ResolveAppend(options.Interceptors, PluginOptions.ClientInterceptorsOption);
        }

        /// <summary>
        /// Handles temporal connection asynchronously.
        /// </summary>
        /// <param name="options">The connection options.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual Task<TemporalConnection> ConnectAsync(
            TemporalClientConnectOptions options,
            Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation)
        {
            return continuation(options);
        }

        /// <summary>
        /// Configures the worker options.
        /// </summary>
        /// <param name="options">The worker options to configure.</param>
        public virtual void ConfigureWorker(TemporalWorkerOptions options)
        {
            DoAppend(options.Activities, PluginOptions.Activities);
            DoAppend(options.Workflows, PluginOptions.Workflows);
            DoAppend(options.NexusServices, PluginOptions.NexusServices);
            options.Interceptors = ResolveAppend(options.Interceptors, PluginOptions.WorkerInterceptorsOption);
            options.WorkflowFailureExceptionTypes = ResolveAppend(options.WorkflowFailureExceptionTypes, PluginOptions.WorkflowFailureExceptionTypesOption);
        }

        /// <summary>
        /// Runs the worker asynchronously.
        /// </summary>
        /// <param name="worker">The worker to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual Task RunWorkerAsync(TemporalWorker worker, Func<TemporalWorker, Task> continuation)
        {
            return continuation(worker);
        }

        /// <summary>
        /// Configures the replayer options.
        /// </summary>
        /// <param name="options">The replayer options to configure.</param>
        public virtual void ConfigureReplayer(WorkflowReplayerOptions options)
        {
            options.DataConverter = Resolve(options.DataConverter, PluginOptions.DataConverterOption);
            DoAppend(options.Workflows, PluginOptions.Workflows);
            options.Interceptors = ResolveAppend(options.Interceptors, PluginOptions.WorkerInterceptorsOption);
            options.WorkflowFailureExceptionTypes = ResolveAppend(options.WorkflowFailureExceptionTypes, PluginOptions.WorkflowFailureExceptionTypesOption);
        }

        /// <summary>
        /// Runs the replayer asynchronously.
        /// </summary>
        /// <param name="replayer">The replayer to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual Task<IEnumerable<WorkflowReplayResult>> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, Task<IEnumerable<WorkflowReplayResult>>> continuation)
        {
            return continuation(replayer);
        }

        private static T Resolve<T>(T existing, SimplePluginOptions.SimplePluginOption<T>? parameter)
        {
            if (parameter == null)
            {
                return existing;
            }
            var option = parameter.Constant ?? existing;
            if (parameter.Configurable != null)
            {
                return parameter.Configurable(option);
            }
            return option;
        }

        private static IReadOnlyCollection<T>? ResolveAppend<T>(
            IReadOnlyCollection<T>? existing,
            SimplePluginOptions.SimplePluginOption<IReadOnlyCollection<T>?>? parameter)
        {
            if (parameter == null)
            {
                return existing;
            }

            var option = existing;
            if (existing != null && parameter.Constant != null)
            {
                option = existing.Concat(parameter.Constant).ToList();
            }
            else if (parameter.Constant != null)
            {
                option = parameter.Constant;
            }
            if (parameter.Configurable != null)
            {
                return parameter.Configurable(option);
            }
            return option;
        }

        private static void DoAppend<T>(
            IList<T> existing,
            IList<T>? parameter)
        {
            if (parameter == null)
            {
                return;
            }

            foreach (var item in parameter)
            {
                existing.Add(item);
            }
        }
    }
}