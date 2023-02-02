using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Worker.Interceptors;

namespace Temporalio.Worker
{
    /// <summary>
    /// Worker for running Temporal workflows and/or activities. This intentionally matches
    /// <c>Microsoft.Extensions.Hosting.BackgroundService</c> structure.
    /// </summary>
    public class TemporalWorker
    {
        private readonly ActivityWorker? activityWorker;
        private int started = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorker"/> class. The options must
        /// have a task queue set and at least one workflow or activity.
        /// </summary>
        /// <param name="client">
        /// Client for this workflow. This is implemented by the commonly used
        /// <see cref="Client.ITemporalClient" />.
        /// </param>
        /// <param name="options">Options for the worker.</param>
        public TemporalWorker(IWorkerClient client, TemporalWorkerOptions options)
        {
            Client = client;
            Options = options;
            BridgeWorker = new(
                (Bridge.Client)client.BridgeClientProvider.BridgeClient,
                client.Options.Namespace,
                options);
            if (options.Activities.Count > 0)
            {
                activityWorker = new(this);
            }
            else
            {
                // TODO(cretz): Remove requirement when workflows are impl'd
                throw new ArgumentException("No activities present");
            }

            // Interceptors are the client interceptors that implement IWorkerInterceptor followed
            // by the explicitly provided ones in options.
            Interceptors = Client.Options.Interceptors?.OfType<IWorkerInterceptor>() ??
                Enumerable.Empty<IWorkerInterceptor>();
            if (Options.Interceptors != null)
            {
                Interceptors = Interceptors.Concat(Options.Interceptors);
            }
        }

        /// <summary>
        /// Gets the options this worker was created with.
        /// </summary>
        public TemporalWorkerOptions Options { get; private init; }

        /// <summary>
        /// Gets the client this worker was created with.
        /// </summary>
        internal IWorkerClient Client { get; private init; }

        /// <summary>
        /// Gets the underlying bridge worker.
        /// </summary>
        internal Bridge.Worker BridgeWorker { get; private init; }

        /// <summary>
        /// Gets the set of interceptors in the order they should be applied.
        /// </summary>
        internal IEnumerable<IWorkerInterceptor> Interceptors { get; private init; }

        /// <summary>
        /// Run this worker until failure or cancelled.
        /// </summary>
        /// <remarks>
        /// This intentionally matches
        /// <c>Microsoft.Extensions.Hosting.BackgroundService.ExecuteAsync</c>.
        /// <para>
        /// When shutting down, the worker will cancel and wait for completion for all executing
        /// activities. If an activity does not properly respond to cancellation, this may never
        /// return.
        /// </para>
        /// </remarks>
        /// <param name="stoppingToken">Cancellation token to stop the worker.</param>
        /// <returns>
        /// Task that will never succeed, only fail. When the task is complete, the worker has
        /// completed shutdown.
        /// </returns>
        /// <exception cref="InvalidOperationException">Already started.</exception>
        /// <exception cref="OperationCanceledException">Cancellation requested.</exception>
        /// <exception cref="Exception">Fatal worker failure.</exception>
        public Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return ExecuteInternalAsync(null, stoppingToken);
        }

        /// <summary>
        /// Run this worker until failure, cancelled, or task from given function completes.
        /// </summary>
        /// <remarks>
        /// When shutting down, the worker will cancel and wait for completion for all executing
        /// activities. If an activity does not properly respond to cancellation, this may never
        /// return.
        /// </remarks>
        /// <param name="untilComplete">
        /// If the task returned from this function completes, the worker will shutdown
        /// (propagating) exception as necessary.
        /// </param>
        /// <param name="stoppingToken">Cancellation token to stop the worker.</param>
        /// <returns>
        /// When the task is complete, the worker has completed shutdown.
        /// </returns>
        /// <exception cref="InvalidOperationException">Already started.</exception>
        /// <exception cref="OperationCanceledException">Cancellation requested.</exception>
        /// <exception cref="Exception">Fatal worker failure.</exception>
        public Task ExecuteAsync(
            Func<Task> untilComplete, CancellationToken stoppingToken = default)
        {
            return ExecuteInternalAsync(untilComplete, stoppingToken);
        }

        /// <summary>
        /// Run this worker until failure, cancelled, or task from given function completes.
        /// </summary>
        /// <remarks>
        /// When shutting down, the worker will cancel and wait for completion for all executing
        /// activities. If an activity does not properly respond to cancellation, this may never
        /// return.
        /// </remarks>
        /// <typeparam name="TResult">Result of given function's task.</typeparam>
        /// <param name="untilComplete">
        /// If the task returned from this function completes, the worker will shutdown
        /// (propagating) exception as necessary.
        /// </param>
        /// <param name="stoppingToken">Cancellation token to stop the worker.</param>
        /// <returns>
        /// When the task is complete, the worker has completed shutdown.
        /// </returns>
        /// <exception cref="InvalidOperationException">Already started.</exception>
        /// <exception cref="OperationCanceledException">Cancellation requested.</exception>
        /// <exception cref="Exception">Fatal worker failure.</exception>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<Task<TResult>> untilComplete, CancellationToken stoppingToken = default)
        {
            TResult? ret = default;
            await ExecuteInternalAsync(
                async () => ret = await untilComplete.Invoke(), stoppingToken);
            return ret!;
        }

        private async Task ExecuteInternalAsync(
            Func<Task>? untilComplete, CancellationToken stoppingToken)
        {
            if (Interlocked.Exchange(ref started, 1) != 0)
            {
                throw new InvalidOperationException("Already started");
            }

            var tasks = new List<Task>()
            {
                // Create a task that will complete on cancellation
                Task.Delay(Timeout.Infinite, stoppingToken),
            };

            // If there is a user-provided task, add it
            var userTask = untilComplete?.Invoke();
            if (userTask != null)
            {
                tasks.Add(userTask);
            }

            // Start workers. We intentionally don't pass cancellation tokens to the individual
            // workers because they are expected to react to polling shutdown and, in the activity
            // worker's case, gracefully shutdown still-running tasks.
            if (activityWorker != null)
            {
                tasks.Add(activityWorker.ExecuteAsync());
            }
            // TODO(cretz): Workflows

            // Wait until any of the tasks complete including cancellation
            var task = await Task.WhenAny(tasks);
            var logger = Client.Options.LoggerFactory.CreateLogger<TemporalWorker>();
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TaskQueue"] = Options.TaskQueue!,
            }))
            {
                if (task == tasks[0])
                {
                    logger.LogInformation("Worker cancelled, shutting down");
                }
                else if (task == userTask)
                {
                    logger.LogInformation("User task completed, shutting down");
                }
                else if (task.Exception != null)
                {
                    logger.LogError(task.Exception, "Worker failed, shutting down");
                }
            }

            // Now wait for all of the tasks to complete. This will collect exceptions from them all
            // and throw them as aggregate.

            // If the token is not already cancelled, we want to remove that from the tasks to be
            // waited on
            if (!tasks[0].IsCompleted)
            {
                tasks.RemoveAt(0);
            }

            // If the user task is not already completed, we want to remove that from the tasks to
            // be waited on
            if (userTask != null && !userTask.IsCompleted)
            {
                tasks.Remove(userTask);
            }

            // Start the shutdown of the worker
            tasks.Add(Task.Run(BridgeWorker.ShutdownAsync));

            // If there is an activity worker, we want to add a graceful shutdown task for it
            if (activityWorker != null)
            {
                tasks.Add(activityWorker.GracefulShutdownAsync());
            }
            try
            {
                // This is essentially guaranteed to throw since it will contain one of the tasks
                // from the WhenAny that threw
                await Task.WhenAll(tasks);
            }
            finally
            {
                try
                {
                    await BridgeWorker.FinalizeShutdownAsync();
                }
                catch (Exception)
                {
                    // Ignore finalization errors, worker will be dropped Rust side anyways
                }
            }
        }
    }
}