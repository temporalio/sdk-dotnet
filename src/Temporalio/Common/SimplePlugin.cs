using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Worker;

#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif

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
        public SimplePlugin(string name, SimplePluginOptions? options = null)
        {
            Name = name;
            Options = options ?? new SimplePluginOptions();
        }

        /// <summary>
        /// Gets the plugin options.
        /// </summary>
        public SimplePluginOptions Options { get; }

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
            options.DataConverter = Resolve(options.DataConverter, Options.DataConverterOption);
            options.Interceptors = ResolveAppend(options.Interceptors, Options.ClientInterceptorsOption);
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
            DoAppend(options.Activities, Options.Activities);
            DoAppend(options.Workflows, Options.Workflows);
            DoAppend(options.NexusServices, Options.NexusServices);
            options.Interceptors = ResolveAppend(options.Interceptors, Options.WorkerInterceptorsOption);
            options.WorkflowFailureExceptionTypes = ResolveAppend(
                options.WorkflowFailureExceptionTypes, Options.WorkflowFailureExceptionTypesOption);
        }

        /// <summary>
        /// Runs the worker asynchronously.
        /// </summary>
        /// <typeparam name="TResult">Result type. For most worker run calls, this is
        /// <see cref="ValueTuple"/>.</typeparam>
        /// <param name="worker">The worker to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <param name="stoppingToken">Cancellation token to stop the worker.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task<TResult> RunWorkerAsync<TResult>(
            TemporalWorker worker,
            Func<TemporalWorker, CancellationToken, Task<TResult>> continuation,
            CancellationToken stoppingToken)
        {
            if (Options.RunContextBefore is { } before)
            {
                await before().ConfigureAwait(false);
            }
            try
            {
                return await continuation(worker, stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                if (Options.RunContextAfter is { } after)
                {
                    await after().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Configures the replayer options.
        /// </summary>
        /// <param name="options">The replayer options to configure.</param>
        public virtual void ConfigureReplayer(WorkflowReplayerOptions options)
        {
            options.DataConverter = Resolve(options.DataConverter, Options.DataConverterOption);
            DoAppend(options.Workflows, Options.Workflows);
            options.Interceptors = ResolveAppend(options.Interceptors, Options.WorkerInterceptorsOption);
            options.WorkflowFailureExceptionTypes = ResolveAppend(
                options.WorkflowFailureExceptionTypes, Options.WorkflowFailureExceptionTypesOption);
        }

        /// <summary>
        /// Runs the replayer asynchronously.
        /// </summary>
        /// <param name="replayer">The replayer to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <param name="cancellationToken">Cancellation token to stop the replay.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task<IEnumerable<WorkflowReplayResult>> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, CancellationToken, Task<IEnumerable<WorkflowReplayResult>>> continuation,
            CancellationToken cancellationToken)
        {
            if (Options.RunContextBefore is { } before)
            {
                await before().ConfigureAwait(false);
            }
            try
            {
                return await continuation(replayer, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (Options.RunContextAfter is { } after)
                {
                    await after().ConfigureAwait(false);
                }
            }
        }

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Runs the replayer asynchronously.
        /// </summary>
        /// <param name="replayer">The replayer to run.</param>
        /// <param name="continuation">The continuation function.</param>
        /// <param name="cancellationToken">Cancellation token to stop the replay.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async IAsyncEnumerable<WorkflowReplayResult> ReplayWorkflowsAsync(
            WorkflowReplayer replayer,
            Func<WorkflowReplayer, IAsyncEnumerable<WorkflowReplayResult>> continuation,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (Options.RunContextBefore is { } before)
            {
                await before().ConfigureAwait(false);
            }
            try
            {
                var asyncEnum = continuation(replayer);
                await foreach (var res in asyncEnum.ConfigureAwait(false).WithCancellation(cancellationToken))
                {
                    yield return res;
                }
            }
            finally
            {
                if (Options.RunContextAfter is { } after)
                {
                    await after().ConfigureAwait(false);
                }
            }
        }
#endif

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