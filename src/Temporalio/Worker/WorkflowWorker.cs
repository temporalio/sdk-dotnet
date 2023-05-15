#pragma warning disable CA1031 // We do want to catch _all_ exceptions in this file sometimes

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Worker for workflows.
    /// </summary>
    internal class WorkflowWorker
    {
        private static readonly TimeSpan DefaultDeadlockTimeout = TimeSpan.FromSeconds(2);

        private readonly WorkflowWorkerOptions options;
        private readonly Action<string, RemoveFromCache>? onEviction;
        private readonly ILogger logger;
        // Keyed by run ID
        private readonly ConcurrentDictionary<string, IWorkflowInstance> runningWorkflows = new();
        // Keyed by run ID
        private readonly ConcurrentDictionary<string, Task> deadlockedWorkflows = new();
        private readonly TimeSpan deadlockTimeout;
        private readonly Func<WorkflowInstanceDetails, IWorkflowInstance> instanceFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowWorker"/> class.
        /// </summary>
        /// <param name="options">Worker options.</param>
        /// <param name="onEviction">Eviction hook.</param>
        public WorkflowWorker(
            WorkflowWorkerOptions options,
            Action<string, RemoveFromCache>? onEviction = null)
        {
            this.options = options;
            this.onEviction = onEviction;
            logger = options.LoggerFactory.CreateLogger<WorkflowWorker>();
            WorkflowDefinitions = new();
            foreach (var defn in options.Workflows)
            {
                if (!defn.Instantiable)
                {
                    throw new ArgumentException($"Workflow named {defn.Name} is not instantiable");
                }
                if (WorkflowDefinitions.ContainsKey(defn.Name))
                {
                    throw new ArgumentException($"Duplicate workflow named {defn.Name}");
                }
                WorkflowDefinitions[defn.Name] = defn;
            }
            deadlockTimeout = options.DebugMode ? Timeout.InfiniteTimeSpan : DefaultDeadlockTimeout;
            instanceFactory = options.WorkflowInstanceFactory ??
                (details => new WorkflowInstance(details, options.LoggerFactory));
        }

        /// <summary>
        /// Gets the known set of workflow definitions by name.
        /// </summary>
        internal Dictionary<string, WorkflowDefinition> WorkflowDefinitions { get; private init; }

        /// <summary>
        /// Execute this worker until poller shutdown or failure. If there is a failure, this may
        /// need to be called a second time after shutdown initiated to ensure workflow activations
        /// are drained.
        /// </summary>
        /// <returns>Task that only completes successfully on poller shutdown.</returns>
        public async Task ExecuteAsync()
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                ["TaskQueue"] = options.TaskQueue,
            }))
            {
                var tasks = new ConcurrentDictionary<Task, bool?>();
                var codec = options.DataConverter.PayloadCodec;
                while (true)
                {
                    var act = await options.BridgeWorker.PollWorkflowActivationAsync().ConfigureAwait(false);
                    // If it's null, we're done
                    if (act == null)
                    {
                        break;
                    }
                    // Otherwise handle and put on task set
                    // TODO(cretz): Any reason for users to need to customize factory here?
                    var task = Task.Run(() => HandleActivationAsync(codec, act));
                    tasks[task] = null;
                    _ = task.ContinueWith(
                        task => { tasks.TryRemove(task, out _); }, TaskScheduler.Current);
                }
                // Wait on every task to complete (exceptions should never happen)
                await Task.WhenAll(tasks.Keys).ConfigureAwait(false);
                var deadlockCount = deadlockedWorkflows.Count;
                if (deadlockCount > 0)
                {
                    logger.LogWarning(
                        "Worker shutdown with {DeadlockedTaskCount} deadlocked workflow tasks still running",
                        deadlockCount);
                }
            }
        }

        private async Task HandleActivationAsync(IPayloadCodec? codec, WorkflowActivation act)
        {
            WorkflowActivationCompletion comp;
            RemoveFromCache? removeJob = null;

            // Catch any exception as a completion failure
            try
            {
                // Decode the activation if there is a codec
                if (codec != null)
                {
                    await WorkflowCodecHelper.DecodeAsync(codec, act).ConfigureAwait(false);
                }

                // Log proto at trace level
                logger.LogTrace("Received workflow activation: {Activation}", act);

                // We only have to run if there are any non-remove jobs
                removeJob = act.Jobs.Select(job => job.RemoveFromCache).FirstOrDefault(job => job != null);
                if (act.Jobs.Count > 1 || removeJob == null)
                {
                    // If the activation never completed before, we need to assume continually
                    // deadlocked and just rethrow the same exception as before
                    if (deadlockedWorkflows.ContainsKey(act.RunId))
                    {
                        throw new InvalidOperationException($"Workflow with ID {act.RunId} deadlocked after {deadlockTimeout}");
                    }

                    // If the workflow is not yet running, create it. We know that we will only get
                    // one activation per workflow at a time, so GetOrAdd is safe for our use.
                    var workflow = runningWorkflows.GetOrAdd(act.RunId, _ => CreateInstance(act));

                    // Activate or timeout with deadlock timeout
                    // TODO(cretz): Any reason for users to need to customize factory here?
                    // TODO(cretz): Since deadlocks can cause unreclaimed threads, we are setting
                    // LongRunning here. That means they may oversubscribe and create a new thread
                    // just for this, is that ok?
                    var workflowTask = Task.Factory.StartNew(
                        () => workflow.Activate(act),
                        default,
                        TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach | TaskCreationOptions.PreferFairness,
                        TaskScheduler.Current);
                    using (var timeoutCancel = new CancellationTokenSource())
                    {
                        // We create a delay task to catch any deadlock timeouts, and if it didn't
                        // deadlock, cancel the task
                        // TODO(cretz): For newer .NET, use WaitAsync which is cheaper
                        var timeoutTask = Task.Delay(deadlockTimeout, timeoutCancel.Token);
                        if (timeoutTask == await Task.WhenAny(workflowTask, timeoutTask).ConfigureAwait(false))
                        {
                            // Hit deadlock timeout, add this to deadlocked set. This is only for the
                            // next activation which will be a remove job.
                            deadlockedWorkflows[act.RunId] = workflowTask;
                            throw new InvalidOperationException(
                                $"Workflow with ID {act.RunId} deadlocked after {deadlockTimeout}");
                        }
                        // Cancel deadlock timeout timer since we didn't hit it
                        timeoutCancel.Cancel();
                    }
                    comp = await workflowTask.ConfigureAwait(false);
                }
                else
                {
                    comp = new() { Successful = new() };
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed handling activation on workflow with run ID {RunId}", act.RunId);
                // Any error here is a completion failure
                comp = new() { Failed = new() };
                try
                {
                    comp.Failed.Failure_ = options.DataConverter.FailureConverter.ToFailure(
                        e, options.DataConverter.PayloadConverter);
                }
                catch (Exception inner)
                {
                    logger.LogError(inner, "Failed converting activation exception on workflow with run ID {RunId}", act.RunId);
                    comp.Failed.Failure_ = new() { Message = $"Failed converting exception: {inner}" };
                }
            }

            // Always set the run ID of the completion
            comp.RunId = act.RunId;

            // Encode the completion if there is a codec
            if (codec != null)
            {
                try
                {
                    await WorkflowCodecHelper.EncodeAsync(codec, comp).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed encoding completion on workflow with run ID {RunId}", act.RunId);
                    comp.Failed = new() { Failure_ = new() { Message = $"Failed encoding completion: {e}" } };
                }
            }

            // Remove from cache if requested
            if (removeJob != null)
            {
                logger.LogDebug(
                    "Evicting workflow with run ID {RunId}, message: {Message}",
                    act.RunId,
                    removeJob.Message);
                runningWorkflows.TryRemove(act.RunId, out _);
                onEviction?.Invoke(act.RunId, removeJob);
                // If this is a deadlocked workflow task, we attach remove completion to task
                // completion
                if (deadlockedWorkflows.TryRemove(act.RunId, out var task))
                {
                    _ = task.ContinueWith(
                        async _ =>
                        {
                            logger.LogDebug(
                                "Notify core of deadlocked workflow eviction for run ID {RunId}",
                                act.RunId);
                            await options.BridgeWorker.CompleteWorkflowActivationAsync(comp).ConfigureAwait(false);
                        },
                        TaskScheduler.Current);
                    // Do not send completion, workflow is deadlocked
                    return;
                }
            }

            // Send completion
            logger.LogTrace("Sending workflow completion: {Completion}", comp);
            try
            {
                await options.BridgeWorker.CompleteWorkflowActivationAsync(comp).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed completing activation on  {RunId}", act.RunId);
            }
        }

        private IWorkflowInstance CreateInstance(WorkflowActivation act)
        {
            var start = act.Jobs.Select(j => j.StartWorkflow).FirstOrDefault(s => s != null);
            if (start == null)
            {
                throw new InvalidOperationException("Missing workflow start (unexpectedly evicted?)");
            }
            if (!WorkflowDefinitions.TryGetValue(start.WorkflowType, out var defn))
            {
                var names = string.Join(", ", WorkflowDefinitions.Keys.OrderBy(s => s));
                throw new ApplicationFailureException(
                    $"Workflow type {start.WorkflowType} is not registered on this worker, available workflows: {names}",
                    "NotFoundError");
            }
            return instanceFactory(
                new(
                    Namespace: options.Namespace,
                    TaskQueue: options.TaskQueue,
                    Definition: defn,
                    InitialActivation: act,
                    Start: start,
                    InboundInterceptorTypes: options.WorkflowInboundInterceptorTypes,
                    PayloadConverterType: options.DataConverter.PayloadConverterType,
                    FailureConverterType: options.DataConverter.FailureConverterType,
                    DisableTracingEvents: options.DisableWorkflowTracingEventListener,
                    WorkflowStackTrace: options.WorkflowStackTrace)
                {
                    // Eagerly set these since we're not in a sandbox so we already have the
                    // instantiated forms
                    PayloadConverter = options.DataConverter.PayloadConverter,
                    FailureConverter = options.DataConverter.FailureConverter,
                });
        }
    }
}