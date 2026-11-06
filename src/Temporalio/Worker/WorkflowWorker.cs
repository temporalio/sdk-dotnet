#pragma warning disable CA1031 // We do want to catch _all_ exceptions in this file sometimes

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCommands;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Common;
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
        private readonly IExternalStorageRunner externalStorage;

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
            externalStorage = ExternalStorageRunner.Create(options.DataConverter.ExternalStorage);
            foreach (var defn in options.Workflows)
            {
                if (!defn.Instantiable)
                {
                    throw new ArgumentException($"Workflow named {defn.Name} is not instantiable. " +
                        "Note, dependency injection is not supported in workflows. " +
                        "Workflows must be deterministic and self-contained with a lifetime controlled by Temporal.");
                }

                if (options.DeploymentOptions?.UseWorkerVersioning == true)
                {
                    if (options.DeploymentOptions?.DefaultVersioningBehavior is null or VersioningBehavior.Unspecified
                        && defn.VersioningBehavior is null or VersioningBehavior.Unspecified)
                    {
                        throw new ArgumentException($"Workflow named {defn.Name} must specify a " +
                            "versioning behavior, since the worker has no default.");
                    }
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

        /// <summary>
        /// Determines the storage target for a single completion command. A workflow task's base
        /// target is the current workflow; the commands handled below name another execution that
        /// reads the payload back. Every command variant is listed so a newly added one fails here
        /// rather than silently inheriting the current workflow.
        /// </summary>
        /// <param name="command">Command whose payloads are being offloaded.</param>
        /// <param name="currentWorkflow">Target for the workflow producing this completion.</param>
        /// <param name="parent">Parent of the current workflow, if it has one.</param>
        /// <returns>Target to offload the command's payloads under, or null for no target.</returns>
        internal static IStorageDriverTargetInfo.Workflow? StorageTargetForCommand(
            WorkflowCommand command,
            IStorageDriverTargetInfo.Workflow? currentWorkflow,
            WorkflowInfo.ParentInfo? parent) =>
            command.VariantCase switch
            {
                // The child being started, which the server has not yet assigned a run to.
                WorkflowCommand.VariantOneofCase.StartChildWorkflowExecution =>
                    new IStorageDriverTargetInfo.Workflow(
                        command.StartChildWorkflowExecution.Namespace,
                        command.StartChildWorkflowExecution.WorkflowId,
                        RunId: null,
                        command.StartChildWorkflowExecution.WorkflowType),

                // The workflow the signal addresses.
                WorkflowCommand.VariantOneofCase.SignalExternalWorkflowExecution =>
                    StorageTargetForSignalExternal(
                        command.SignalExternalWorkflowExecution, currentWorkflow),

                // The successor run, which has the same id, no run yet, and a type only when set.
                WorkflowCommand.VariantOneofCase.ContinueAsNewWorkflowExecution =>
                    currentWorkflow is null ? null : currentWorkflow with
                    {
                        RunId = null,
                        WorkflowType = NullIfEmpty(command.ContinueAsNewWorkflowExecution.WorkflowType),
                    },

                // A child's result is read by its parent.
                WorkflowCommand.VariantOneofCase.CompleteWorkflowExecution when parent is not null =>
                    new IStorageDriverTargetInfo.Workflow(
                        parent.Namespace, parent.WorkflowId, parent.RunId, WorkflowType: null),

                // No Nexus target kind exists yet, so a Nexus operation offloads without one.
                WorkflowCommand.VariantOneofCase.ScheduleNexusOperation => null,

                // The remaining commands read their payloads back as the current workflow, or
                // carry none and so never reach a driver.
                WorkflowCommand.VariantOneofCase.None or
                WorkflowCommand.VariantOneofCase.StartTimer or
                WorkflowCommand.VariantOneofCase.ScheduleActivity or
                WorkflowCommand.VariantOneofCase.RespondToQuery or
                WorkflowCommand.VariantOneofCase.RequestCancelActivity or
                WorkflowCommand.VariantOneofCase.CancelTimer or
                WorkflowCommand.VariantOneofCase.CompleteWorkflowExecution or
                WorkflowCommand.VariantOneofCase.FailWorkflowExecution or
                WorkflowCommand.VariantOneofCase.CancelWorkflowExecution or
                WorkflowCommand.VariantOneofCase.SetPatchMarker or
                WorkflowCommand.VariantOneofCase.CancelChildWorkflowExecution or
                WorkflowCommand.VariantOneofCase.RequestCancelExternalWorkflowExecution or
                WorkflowCommand.VariantOneofCase.CancelSignalWorkflow or
                WorkflowCommand.VariantOneofCase.ScheduleLocalActivity or
                WorkflowCommand.VariantOneofCase.RequestCancelLocalActivity or
                WorkflowCommand.VariantOneofCase.UpsertWorkflowSearchAttributes or
                WorkflowCommand.VariantOneofCase.ModifyWorkflowProperties or
                WorkflowCommand.VariantOneofCase.UpdateResponse or
                WorkflowCommand.VariantOneofCase.RequestCancelNexusOperation => currentWorkflow,

                _ => throw new InvalidOperationException(
                    $"Workflow command {command.VariantCase} has no assigned external storage target"),
            };

        /// <summary>
        /// Determines the storage target for a signal to an external workflow, which is the workflow
        /// the signal addresses. Every target variant is listed so a newly added one fails here.
        /// </summary>
        /// <param name="signal">Signal command whose payloads are being offloaded.</param>
        /// <param name="currentWorkflow">Target for the workflow sending the signal.</param>
        /// <returns>Target to offload the signal's payloads under, or null for no target.</returns>
        internal static IStorageDriverTargetInfo.Workflow? StorageTargetForSignalExternal(
            SignalExternalWorkflowExecution signal,
            IStorageDriverTargetInfo.Workflow? currentWorkflow) =>
            signal.TargetCase switch
            {
                SignalExternalWorkflowExecution.TargetOneofCase.WorkflowExecution =>
                    new IStorageDriverTargetInfo.Workflow(
                        signal.WorkflowExecution.Namespace,
                        signal.WorkflowExecution.WorkflowId,
                        RunId: NullIfEmpty(signal.WorkflowExecution.RunId),
                        WorkflowType: null),

                // A child addressed by id alone, in the current workflow's namespace.
                SignalExternalWorkflowExecution.TargetOneofCase.ChildWorkflowId =>
                    currentWorkflow is null ? null : currentWorkflow with
                    {
                        WorkflowId = signal.ChildWorkflowId,
                        RunId = null,
                        WorkflowType = null,
                    },

                // The signal addresses no one, so there is no execution to target.
                SignalExternalWorkflowExecution.TargetOneofCase.None => null,

                _ => throw new InvalidOperationException(
                    $"Signal to external workflow has an unhandled target {signal.TargetCase}"),
            };

        // A proto string field is the empty string when unset; a target field is null when unknown.
        private static string? NullIfEmpty(string value) =>
            string.IsNullOrEmpty(value) ? null : value;

        // An external storage failure reports its own cause; any other failure leaves it unspecified.
        private static Api.Enums.V1.WorkflowTaskFailedCause ForceCauseForFailure(Exception e) =>
            e is ExternalStorageException
                ? Api.Enums.V1.WorkflowTaskFailedCause.ExternalStorageFailure
                : Api.Enums.V1.WorkflowTaskFailedCause.Unspecified;

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
            DataConverter dataConverterNoContext = options.DataConverter;
            DataConverter? dataConverterWorkflowContext = null;
            WorkflowCodecHelper.WorkflowCodecContext? codecContext = null;
            // The current workflow's identity, kept for building per-command storage targets after
            // the try block. Null only if the activation could not be associated with a workflow.
            string? completionWorkflowId = null;
            string? completionWorkflowType = null;

            // Catch any exception as a completion failure
            try
            {
                // Create data converter with context before doing any work
                string workflowId, workflowType;
                IWorkflowCodecHelperInstance? instanceForCodec;
                if (runningWorkflows.TryGetValue(act.RunId, out var inst))
                {
                    workflowId = inst.Info.WorkflowId;
                    workflowType = inst.Info.WorkflowType;
                    instanceForCodec = inst;
                }
                else if (act.Jobs.Select(j => j.InitializeWorkflow).FirstOrDefault(s => s != null) is { } initJob)
                {
                    workflowId = initJob.WorkflowId;
                    workflowType = initJob.WorkflowType;
                    instanceForCodec = null;
                }
                else
                {
                    throw new InvalidOperationException("Missing workflow start (unexpectedly evicted?)");
                }
                completionWorkflowId = workflowId;
                completionWorkflowType = workflowType;
                dataConverterWorkflowContext = dataConverterNoContext.WithSerializationContext(
                    new ISerializationContext.Workflow(options.Namespace, WorkflowId: workflowId));
                // We'll only apply codec if one of the two converters has one
                if (dataConverterNoContext.PayloadCodec != null ||
                    dataConverterWorkflowContext.PayloadCodec != null)
                {
                    codecContext = new(
                        CodecNoContext: dataConverterNoContext.PayloadCodec,
                        CodecWorkflowContext: dataConverterWorkflowContext.PayloadCodec,
                        Namespace: options.Namespace,
                        WorkflowId: workflowId,
                        WorkflowType: workflowType,
                        TaskQueue: options.TaskQueue,
                        Instance: instanceForCodec);
                }
                // External storage is independent of the codec, so this runs even when there is none.
                await externalStorage.RetrieveAsync(
                    PayloadVisitors.VisitAsync, act, CancellationToken.None).ConfigureAwait(false);
                if (codecContext != null)
                {
                    await WorkflowCodecHelper.DecodeAsync(codecContext, act).ConfigureAwait(false);
                }

                // Log proto at trace level
                logger.LogTrace("Received workflow activation: {Activation}", act);

                // If the activation never completed before, we need to assume continually
                // deadlocked and just rethrow the same exception as before
                if (deadlockedWorkflows.ContainsKey(act.RunId))
                {
                    throw new InvalidOperationException($"[TMPRL1101] Workflow with run ID {act.RunId} deadlocked after {deadlockTimeout}");
                }

                // If the workflow is not yet running, create it. We know that we will only get
                // one activation per workflow at a time, so GetOrAdd is safe for our use.
                var workflow = runningWorkflows.GetOrAdd(act.RunId, _ => CreateInstance(
                    act, dataConverterNoContext, dataConverterWorkflowContext));
                if (codecContext != null)
                {
                    codecContext = codecContext with { Instance = workflow };
                }

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
                            $"[TMPRL1101] Workflow with run ID {act.RunId} deadlocked after {deadlockTimeout}");
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
                    // Failure converter needs to be in workflow context if available
                    var dataConverterForFailure = dataConverterWorkflowContext ?? dataConverterNoContext;
                    comp.Failed.Failure_ = dataConverterForFailure.FailureConverter.ToFailure(
                        e, dataConverterForFailure.PayloadConverter);
                }
                catch (Exception inner)
                {
                    logger.LogError(inner, "Failed converting activation exception on workflow with run ID {RunId}", act.RunId);
                    comp.Failed.Failure_ = new() { Message = $"Failed converting exception: {inner}" };
                }
                comp.Failed.ForceCause = ForceCauseForFailure(e);
            }

            // Always set the run ID of the completion
            comp.RunId = act.RunId;

            // Encode the completion if there is a codec context
            if (codecContext != null)
            {
                try
                {
                    await WorkflowCodecHelper.EncodeAsync(codecContext, comp).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed encoding completion on workflow with run ID {RunId}", act.RunId);
                    comp.Failed = new() { Failure_ = new() { Message = $"Failed encoding completion: {e}" } };
                }
            }

            // External storage is independent of the codec, so this runs even when there is none.
            try
            {
                var completionTarget = completionWorkflowId == null ? null :
                    new IStorageDriverTargetInfo.Workflow(
                        options.Namespace, completionWorkflowId, act.RunId, completionWorkflowType);
                // A continue-as-new successor inherits the parent info but does not deliver its
                // completion to that parent, so only a run that is not a successor targets it.
                var parent = runningWorkflows.TryGetValue(act.RunId, out var instForTarget) &&
                    instForTarget.Info.ContinuedRunId == null
                    ? instForTarget.Info.Parent : null;
                await StoreCompletionPayloadsAsync(comp, completionTarget, parent)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed storing completion payloads on workflow with run ID {RunId}", act.RunId);
                comp.Failed = new() { ForceCause = ForceCauseForFailure(e) };
                try
                {
                    // Encode the failure through the codec but skip offload: the offload just
                    // failed, and if this was not a storage fault it was a bug in the offload path
                    // that would recur.
                    var dataConverterForFailure = dataConverterWorkflowContext ?? dataConverterNoContext;
                    comp.Failed.Failure_ = await dataConverterForFailure.ToFailureAsync(
                        e, ExternalStorageContext.For(dataConverterForFailure, skipStore: true))
                        .ConfigureAwait(false);
                }
                catch (Exception inner)
                {
                    logger.LogError(inner, "Failed converting storage exception on workflow with run ID {RunId}", act.RunId);
                    comp.Failed.Failure_ = new() { Message = $"Failed storing completion payloads: {e}" };
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

        // Offloads a completion one command at a time, each under the target naming the execution
        // that reads its payloads. The whole-completion failure takes the current workflow.
        private async Task StoreCompletionPayloadsAsync(
            WorkflowActivationCompletion comp,
            IStorageDriverTargetInfo.Workflow? currentWorkflow,
            WorkflowInfo.ParentInfo? parent)
        {
            if (comp.Successful != null)
            {
                foreach (var command in comp.Successful.Commands)
                {
                    var target = StorageTargetForCommand(command, currentWorkflow, parent);
                    await externalStorage.StoreAsync(
                        PayloadVisitors.VisitAsync, command, target, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }

            if (comp.Failed != null)
            {
                await externalStorage.StoreAsync(
                    PayloadVisitors.VisitAsync, comp.Failed, currentWorkflow, CancellationToken.None)
                    .ConfigureAwait(false);
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

        private IWorkflowInstance CreateInstance(
            WorkflowActivation act,
            DataConverter dataConverterNoContext,
            DataConverter dataConverterWorkflowContext)
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
                    PayloadConverterNoContext: dataConverterNoContext.PayloadConverter,
                    PayloadConverterWorkflowContext: dataConverterWorkflowContext.PayloadConverter,
                    FailureConverterNoContext: dataConverterNoContext.FailureConverter,
                    FailureConverterWorkflowContext: dataConverterWorkflowContext.FailureConverter,
                    LoggerFactory: options.LoggerFactory,
                    DisableTracingEvents: options.DisableWorkflowTracingEventListener,
                    WorkflowStackTrace: options.WorkflowStackTrace,
                    OnTaskStarting: options.OnTaskStarting,
                    OnTaskCompleted: options.OnTaskCompleted,
                    RuntimeMetricMeter: options.RuntimeMetricMeter,
                    WorkerLevelFailureExceptionTypes: options.WorkerLevelFailureExceptionTypes,
                    DisableEagerActivityExecution: options.DisableEagerActivityExecution,
                    PatchActivationCallback: options.PatchActivationCallback,
                    AssertValidLocalActivity: options.AssertValidLocalActivity));
        }
    }
}
