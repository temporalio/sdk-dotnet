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
    public class TemporalWorker : IDisposable
    {
        private readonly ActivityWorker? activityWorker;
        private readonly WorkflowWorker? workflowWorker;
        private readonly bool workflowTracingEventListenerEnabled;
        private int started;

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
            // Clone the options to discourage mutation (but we aren't completely disabling mutation
            // on the Options field herein).
            Options = (TemporalWorkerOptions)options.Clone();
            BridgeWorker = new(
                (Bridge.Client)client.BridgeClientProvider.BridgeClient,
                client.Options.Namespace,
                options);
            if (options.Activities.Count == 0 && options.Workflows.Count == 0)
            {
                throw new ArgumentException("Must have at least one workflow and/or activity");
            }

            // Interceptors are the client interceptors that implement IWorkerInterceptor followed
            // by the explicitly provided ones in options.
            Interceptors = Client.Options.Interceptors?.OfType<IWorkerInterceptor>() ??
                Enumerable.Empty<IWorkerInterceptor>();
            if (Options.Interceptors != null)
            {
                Interceptors = Interceptors.Concat(Options.Interceptors);
            }
            // Extract workflow interceptor constructors out
            var expectedTypes = new Type[] { typeof(WorkflowInboundInterceptor) };
            var workflowInboundInterceptorTypes = Interceptors.Select(
                i =>
                {
                    var type = i.WorkflowInboundInterceptorType;
                    if (type == null)
                    {
                        return null;
                    }
                    else if (type.GetConstructor(expectedTypes) == null)
                    {
                        throw new InvalidOperationException($"Workflow interceptor {type} missing constructor accepting inbound");
                    }
                    return type;
                }).OfType<Type>();

            // Enable workflow task tracing if needed
            workflowTracingEventListenerEnabled =
                !options.DisableWorkflowTracingEventListener && workflowWorker != null;
            if (workflowTracingEventListenerEnabled)
            {
                WorkflowTracingEventListener.Instance.Register();
            }

            // Create workers
            if (options.Activities.Count > 0)
            {
                activityWorker = new(this);
            }
            if (options.Workflows.Count > 0)
            {
                workflowWorker = new(new(
                    BridgeWorker: BridgeWorker,
                    Namespace: client.Options.Namespace,
                    TaskQueue: options.TaskQueue!,
                    Workflows: options.Workflows,
                    DataConverter: client.Options.DataConverter,
                    WorkflowInboundInterceptorTypes: workflowInboundInterceptorTypes,
                    LoggerFactory: options.LoggerFactory ?? client.Options.LoggerFactory,
                    WorkflowInstanceFactory: options.WorkflowInstanceFactory,
                    DebugMode: options.DebugMode,
                    DisableWorkflowTracingEventListener: options.DisableWorkflowTracingEventListener,
                    WorkflowStackTrace: options.WorkflowStackTrace));
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="TemporalWorker"/> class.
        /// </summary>
        ~TemporalWorker() => Dispose(false);

        /// <summary>
        /// Gets the options this worker was created with.
        /// </summary>
        public TemporalWorkerOptions Options { get; private init; }

        /// <summary>
        /// Gets the client this worker was created with.
        /// </summary>
        internal IWorkerClient Client { get; private init; }

        /// <summary>
        /// Gets or sets the underlying bridge worker.
        /// </summary>
        /// <remarks>
        /// Setter only visible for testing.
        /// </remarks>
        internal Bridge.Worker BridgeWorker { get; set; }

        /// <summary>
        /// Gets the set of interceptors in the order they should be applied.
        /// </summary>
        internal IEnumerable<IWorkerInterceptor> Interceptors { get; private init; }

        /// <summary>
        /// Gets the logger factory.
        /// </summary>
        internal ILoggerFactory LoggerFactory =>
            Options.LoggerFactory ?? Client.Options.LoggerFactory;

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
        public Task ExecuteAsync(CancellationToken stoppingToken) =>
            ExecuteInternalAsync(null, stoppingToken);

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
        /// propagating exception as necessary.
        /// </param>
        /// <param name="stoppingToken">Cancellation token to stop the worker.</param>
        /// <returns>
        /// When the task is complete, the worker has completed shutdown.
        /// </returns>
        /// <exception cref="InvalidOperationException">Already started.</exception>
        /// <exception cref="OperationCanceledException">Cancellation requested.</exception>
        /// <exception cref="Exception">Fatal worker failure.</exception>
        public Task ExecuteAsync(
            Func<Task> untilComplete, CancellationToken stoppingToken = default) =>
            ExecuteInternalAsync(untilComplete, stoppingToken);

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
                async () => ret = await untilComplete.Invoke().ConfigureAwait(false),
                stoppingToken).ConfigureAwait(false);
            return ret!;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose the worker.
        /// </summary>
        /// <param name="disposing">Whether disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                activityWorker?.Dispose();
                BridgeWorker.Dispose();
                // Remove task tracing if not disabled and there are workflows present
                if (workflowTracingEventListenerEnabled)
                {
                    WorkflowTracingEventListener.Instance.Unregister();
                }
            }
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
            var pollTasks = new List<Task>();
            Task? activityWorkerTask = null;
            if (activityWorker != null)
            {
                activityWorkerTask = activityWorker.ExecuteAsync();
                tasks.Add(activityWorkerTask);
                pollTasks.Add(activityWorkerTask);
            }
            Task? workflowWorkerTask = null;
            if (workflowWorker != null)
            {
                workflowWorkerTask = workflowWorker.ExecuteAsync();
                tasks.Add(workflowWorkerTask);
                pollTasks.Add(workflowWorkerTask);
            }

            // Wait until any of the tasks complete including cancellation
            var task = await Task.WhenAny(tasks).ConfigureAwait(false);
            var logger = LoggerFactory.CreateLogger<TemporalWorker>();
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

            // Start shutdown
            BridgeWorker.InitiateShutdown();

            // If the poll task actually completed with an exception to cause this shutdown, we need
            // to run that poll task again after shutdown since it needs to handle post-shutdown
            // messages. We do this my simply adding another execute to poll tasks. The regular task
            // list will still have the original one that threw which we need later.
            if (activityWorkerTask?.Exception != null)
            {
                pollTasks.Add(activityWorker!.ExecuteAsync());
            }
            if (workflowWorkerTask?.Exception != null)
            {
                pollTasks.Add(workflowWorker!.ExecuteAsync());
            }

            // Tell all activities the worker is shutting down
            activityWorker?.NotifyShutdown();

            // Wait for all poll tasks to be complete. We ignore the exception because the original
            // poll tasks remain in the regular task list that will be waited again below.
#pragma warning disable CA1031 // Intentionally swallow all exceptions
            try
            {
                await Task.WhenAll(pollTasks).ConfigureAwait(false);
            }
            catch
            {
            }
#pragma warning restore CA1031

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

            // Now wait for all of the tasks to complete. This will collect exceptions from them all
            // and throw them as aggregate.
            try
            {
                // Unless the user-provided task completed successfully, this is essentially
                // guaranteed to throw since it will contain one of the tasks from the WhenAny
                // that threw
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
#pragma warning disable CA1031 // Intentionally swallow all exceptions to finalizing shutdown
                try
                {
                    await BridgeWorker.FinalizeShutdownAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // Ignore finalization errors, worker will be dropped Rust side anyways
                    logger.LogWarning(e, "Worker finalization failed");
                }
#pragma warning restore CA1031
            }
        }
    }
}