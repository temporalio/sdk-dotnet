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
        private readonly Dictionary<string, WorkflowDefinition> workflows = new();
        private readonly WorkflowDefinition? dynamicWorkflow;

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
            foreach (var defn in options.Workflows)
            {
                if (!defn.Instantiable)
                {
                    throw new ArgumentException($"Workflow named {defn.Name} is not instantiable. " +
                        "Note, dependency injection is not supported in workflows. " +
                        "Workflows must be deterministic and self-contained with a lifetime controlled by Temporal.");
                }
                if (defn.Name == null)
                {
                    if (dynamicWorkflow != null)
                    {
                        throw new ArgumentException("Multiple dynamic workflows provided");
                    }
                    dynamicWorkflow = defn;
                }
                else if (workflows.ContainsKey(defn.Name))
                {
                    throw new ArgumentException($"Duplicate workflow named {defn.Name}");
                }
                else
                {
                    workflows[defn.Name] = defn;
                }
            }
            deadlockTimeout = options.DebugMode ? Timeout.InfiniteTimeSpan : DefaultDeadlockTimeout;
        }

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
                    var task = Task.Run(() => HandleActivationAsync(act));
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

        private async Task HandleActivationAsync(WorkflowActivation act)
        {
            // Do eviction if requested and exit early
            if (act.Jobs.Select(job => job.RemoveFromCache).FirstOrDefault(job => job != null) is { } removeJob)
            {
                if (act.Jobs.Count != 1)
                {
                    logger.LogWarning("Unexpected job alongside remove job");
                }
                await HandleCacheEvictionAsync(act, removeJob).ConfigureAwait(false);
                return;
            }

            WorkflowActivationCompletion comp;
            DataConverter dataConverter = options.DataConverter;
            WorkflowCodecHelper.WorkflowCodecContext? codecContext = null;

            // Catch any exception as a completion failure
            try
            {
                // Create data converter with context before doing any work
                if (runningWorkflows.TryGetValue(act.RunId, out var inst))
                {
                    codecContext = new(
                        Namespace: options.Namespace,
                        WorkflowId: inst.Info.WorkflowId,
                        WorkflowType: inst.Info.WorkflowType,
                        TaskQueue: options.TaskQueue,
                        Instance: inst);
                }
                else if (act.Jobs.Select(j => j.InitializeWorkflow).FirstOrDefault(s => s != null) is { } initJob)
                {
                    codecContext = new(
                        Namespace: options.Namespace,
                        WorkflowId: initJob.WorkflowId,
                        WorkflowType: initJob.WorkflowType,
                        TaskQueue: options.TaskQueue,
                        Instance: null);
                }
                else
                {
                    throw new InvalidOperationException("Missing workflow start (unexpectedly evicted?)");
                }
                dataConverter = dataConverter.WithSerializationContext(
                    new ISerializationContext.Workflow(
                        Namespace: codecContext.Namespace, WorkflowId: codecContext.WorkflowId));

                // Decode the activation if there is a codec
                if (dataConverter.PayloadCodec is { } decodeCodec)
                {
                    await WorkflowCodecHelper.DecodeAsync(decodeCodec, codecContext, act).ConfigureAwait(false);
                }

                // Log proto at trace level
                logger.LogTrace("Received workflow activation: {Activation}", act);

                // If the activation never completed before, we need to assume continually
                // deadlocked and just rethrow the same exception as before
                if (deadlockedWorkflows.ContainsKey(act.RunId))
                {
                    throw new InvalidOperationException($"[TMPRL1101] Workflow with ID {act.RunId} deadlocked after {deadlockTimeout}");
                }

                // If the workflow is not yet running, create it. We know that we will only get
                // one activation per workflow at a time, so GetOrAdd is safe for our use.
                var workflow = runningWorkflows.GetOrAdd(act.RunId, _ => CreateInstance(act, dataConverter));
                codecContext = codecContext with { Instance = workflow };

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
                            $"[TMPRL1101] Workflow with ID {act.RunId} deadlocked after {deadlockTimeout}");
                    }
                    // Cancel deadlock timeout timer since we didn't hit it
                    timeoutCancel.Cancel();
                }
                comp = await workflowTask.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed handling activation on workflow with run ID {RunId}", act.RunId);
                // Any error here is a completion failure
                comp = new() { Failed = new() };
                try
                {
                    // Failure converter needs to be in workflow context
                    comp.Failed.Failure_ = dataConverter.FailureConverter.ToFailure(
                        e, dataConverter.PayloadConverter);
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
            if (dataConverter.PayloadCodec is { } encodeCodec && codecContext is { } encodeContext)
            {
                try
                {
                    await WorkflowCodecHelper.EncodeAsync(encodeCodec, encodeContext, comp).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed encoding completion on workflow with run ID {RunId}", act.RunId);
                    comp.Failed = new() { Failure_ = new() { Message = $"Failed encoding completion: {e}" } };
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
                logger.LogError(e, "Failed completing activation on {RunId}", act.RunId);
            }
        }

        private async Task HandleCacheEvictionAsync(WorkflowActivation act, RemoveFromCache job)
        {
            logger.LogDebug(
                "Evicting workflow with run ID {RunId}, message: {Message}",
                act.RunId,
                job.Message);
            runningWorkflows.TryRemove(act.RunId, out _);
            onEviction?.Invoke(act.RunId, job);
            var comp = new WorkflowActivationCompletion() { RunId = act.RunId, Successful = new() };
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
                        try
                        {
                            await options.BridgeWorker.CompleteWorkflowActivationAsync(comp).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, "Failed completing eviction on {RunId}", act.RunId);
                        }
                    },
                    TaskScheduler.Current);
                // Do not send completion, workflow is deadlocked
                return;
            }
            // Send eviction completion
            try
            {
                await options.BridgeWorker.CompleteWorkflowActivationAsync(comp).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed completing eviction on {RunId}", act.RunId);
            }
        }

        private IWorkflowInstance CreateInstance(WorkflowActivation act, DataConverter dataConverter)
        {
            var init = act.Jobs.Select(j => j.InitializeWorkflow).FirstOrDefault(s => s != null) ??
                throw new InvalidOperationException("Missing workflow start (unexpectedly evicted?)");
            if (!workflows.TryGetValue(init.WorkflowType, out var defn))
            {
                defn = dynamicWorkflow;
                if (defn == null)
                {
                    var names = string.Join(", ", workflows.Keys.OrderBy(s => s));
                    throw new ApplicationFailureException(
                        $"Workflow type {init.WorkflowType} is not registered on this worker, available workflows: {names}",
                        "NotFoundError");
                }
            }
            return options.WorkflowInstanceFactory(
                new(
                    Namespace: options.Namespace,
                    TaskQueue: options.TaskQueue,
                    Definition: defn,
                    InitialActivation: act,
                    Init: init,
                    Interceptors: options.Interceptors,
                    PayloadConverter: dataConverter.PayloadConverter,
                    FailureConverter: dataConverter.FailureConverter,
                    LoggerFactory: options.LoggerFactory,
                    DisableTracingEvents: options.DisableWorkflowTracingEventListener,
                    WorkflowStackTrace: options.WorkflowStackTrace,
                    OnTaskStarting: options.OnTaskStarting,
                    OnTaskCompleted: options.OnTaskCompleted,
                    RuntimeMetricMeter: options.RuntimeMetricMeter,
                    WorkerLevelFailureExceptionTypes: options.WorkerLevelFailureExceptionTypes,
                    DisableEagerActivityExecution: options.DisableEagerActivityExecution));
        }
    }
}
