#pragma warning disable CA1001 // We know that we have a cancellation token source we instead clean on destruct
#pragma warning disable CA1031 // We do want to catch _all_ exceptions in this file sometimes

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using NexusRpc;
using Temporalio.Api.Common.V1;
using Temporalio.Bridge.Api.ActivityResult;
using Temporalio.Bridge.Api.ChildWorkflow;
using Temporalio.Bridge.Api.Nexus;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCommands;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Runtime;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Instance of a workflow execution.
    /// </summary>
    internal class WorkflowInstance : TaskScheduler, IWorkflowInstance, IWorkflowContext, IWorkflowCodecHelperInstance
    {
        private static readonly string[] Newlines = new[] { "\r", "\n", "\r\n" };
        private static readonly AsyncLocal<WorkflowUpdateInfo> CurrentUpdateInfoLocal = new();

        private readonly TaskFactory taskFactory;
        private readonly IPayloadConverter payloadConverterNoContext;
        private readonly IPayloadConverter payloadConverterWorkflowContext;
        private readonly IFailureConverter failureConverterNoContext;
        private readonly IFailureConverter failureConverterWorkflowContext;
        private readonly Lazy<MetricMeter> metricMeter;
        private readonly Lazy<WorkflowInboundInterceptor> inbound;
        private readonly Lazy<WorkflowOutboundInterceptor> outbound;
        // Lazily created if asked for by user
        private readonly Lazy<NotifyOnSetDictionary<string, WorkflowQueryDefinition>> mutableQueries;
        // Lazily created if asked for by user
        private readonly Lazy<NotifyOnSetDictionary<string, WorkflowSignalDefinition>> mutableSignals;
        // Lazily created if asked for by user
        private readonly Lazy<NotifyOnSetDictionary<string, WorkflowUpdateDefinition>> mutableUpdates;
        private readonly Lazy<Dictionary<string, IRawValue>> memo;
        private readonly Lazy<SearchAttributeCollection> typedSearchAttributes;
        private readonly LinkedList<Task> scheduledTasks = new();
        private readonly Dictionary<Task, LinkedListNode<Task>> scheduledTaskNodes = new();
        private readonly Dictionary<uint, TaskCompletionSource<object?>> timersPending = new();
        private readonly Dictionary<uint, PendingActivityInfo> activitiesPending = new();
        private readonly Dictionary<uint, PendingChildInfo> childWorkflowsPending = new();
        private readonly Dictionary<uint, PendingExternalSignal> externalSignalsPending = new();
        private readonly Dictionary<uint, PendingExternalCancel> externalCancelsPending = new();
        private readonly Dictionary<uint, PendingNexusOperationInfo> nexusOperationsPending = new();
        // Buffered signals have to be a list instead of a dictionary because when a dynamic signal
        // handler is added, we need to traverse in insertion order
        private readonly List<SignalWorkflow> bufferedSignals = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly LinkedList<Tuple<Func<bool>, TaskCompletionSource<object?>>> conditions = new();
        private readonly HashSet<string> patchesNotified = new();
        private readonly Dictionary<string, bool> patchesMemoized = new();
        private readonly WorkflowStackTrace workflowStackTrace;
        // Only non-null if stack trace is not None
        private readonly LinkedList<System.Diagnostics.StackTrace>? pendingTaskStackTraces;
        private readonly ILogger logger;
        private readonly ReplaySafeLogger replaySafeLogger;
        private readonly Action<WorkflowInstance> onTaskStarting;
        private readonly Action<WorkflowInstance, Exception?> onTaskCompleted;
        private readonly IReadOnlyCollection<Type>? workerLevelFailureExceptionTypes;
        private readonly bool disableEagerActivityExecution;
        private readonly Handlers inProgressHandlers = new();
        private readonly WorkflowDefinitionOptions definitionOptions;
        private WorkflowActivationCompletion? completion;
        // Will be set to null after last use (i.e. when workflow actually started)
        private Lazy<object?[]>? startArgs;
        private object? instance;
        private Exception? currentActivationException;
        private uint timerCounter;
        private uint activityCounter;
        private uint childWorkflowCounter;
        private uint nexusOperationCounter;
        private uint externalSignalsCounter;
        private uint externalCancelsCounter;
        private WorkflowQueryDefinition? dynamicQuery;
        private WorkflowSignalDefinition? dynamicSignal;
        private WorkflowUpdateDefinition? dynamicUpdate;
        private bool workflowInitialized;
        private bool applyModernEventLoopLogic;
        private bool dynamicOptionsGetterInvoked;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowInstance"/> class.
        /// </summary>
        /// <param name="details">Immutable details about the instance.</param>
        public WorkflowInstance(WorkflowInstanceDetails details)
        {
            taskFactory = new(default, TaskCreationOptions.None, TaskContinuationOptions.ExecuteSynchronously, this);
            Definition = details.Definition;
            dynamicQuery = Definition.DynamicQuery;
            dynamicSignal = Definition.DynamicSignal;
            dynamicUpdate = Definition.DynamicUpdate;
            payloadConverterNoContext = details.PayloadConverterNoContext;
            payloadConverterWorkflowContext = details.PayloadConverterWorkflowContext;
            failureConverterNoContext = details.FailureConverterNoContext;
            failureConverterWorkflowContext = details.FailureConverterWorkflowContext;
            var rootInbound = new InboundImpl(this);
            // Lazy so it can have the context when instantiating
            inbound = new(
                () =>
                {
                    var ret = details.Interceptors.Reverse().Aggregate(
                        (WorkflowInboundInterceptor)rootInbound,
                        (v, impl) => impl.InterceptWorkflow(v));
                    ret.Init(new OutboundImpl(this));
                    return ret;
                },
                false);
            outbound = new(
                () =>
                {
                    // Must get inbound first
                    _ = inbound.Value;
                    return rootInbound.Outbound!;
                },
                false);
            mutableQueries = new(() => new(Definition.Queries, OnQueryDefinitionAdded), false);
            mutableSignals = new(() => new(Definition.Signals, OnSignalDefinitionAdded), false);
            mutableUpdates = new(() => new(Definition.Updates, OnUpdateDefinitionAdded), false);
            var initialMemo = details.Init.Memo;
            memo = new(
                () => initialMemo == null ? new Dictionary<string, IRawValue>(0) :
                    initialMemo.Fields.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (IRawValue)new RawValue(kvp.Value)),
                false);
            var initialSearchAttributes = details.Init.SearchAttributes;
            typedSearchAttributes = new(
                () => initialSearchAttributes == null ? new() :
                    SearchAttributeCollection.FromProto(initialSearchAttributes),
                false);
            var act = details.InitialActivation;
            if (act.DeploymentVersionForCurrentTask != null)
            {
                CurrentDeploymentVersion = WorkerDeploymentVersion.FromBridge(act.DeploymentVersionForCurrentTask);
            }
            var start = details.Init;
            startArgs = new(
                () => DecodeArgs(
                    method: Definition.RunMethod,
                    payloads: start.Arguments,
                    itemName: $"Workflow {start.WorkflowType}",
                    dynamic: Definition.Dynamic),
                false);
            metricMeter = new(() =>
                new ReplaySafeMetricMeter(
                    details.RuntimeMetricMeter.Value.WithTags(new Dictionary<string, object>()
                    {
                        { "namespace", details.Namespace },
                        { "task_queue", details.TaskQueue },
                        { "workflow_type", start.WorkflowType },
                    })));
            initialSearchAttributes = details.Init.SearchAttributes;
            WorkflowInfo.ParentInfo? parent = null;
            if (start.ParentWorkflowInfo != null)
            {
                parent = new(
                    Namespace: start.ParentWorkflowInfo.Namespace,
                    RunId: start.ParentWorkflowInfo.RunId,
                    WorkflowId: start.ParentWorkflowInfo.WorkflowId);
            }
            WorkflowInfo.RootInfo? root = null;
            if (start.RootWorkflow != null)
            {
                root = new(
                    RunId: start.RootWorkflow.RunId,
                    WorkflowId: start.RootWorkflow.WorkflowId);
            }
            var lastFailure = start.ContinuedFailure == null ?
                null : failureConverterWorkflowContext.ToException(
                    start.ContinuedFailure, payloadConverterWorkflowContext);
            var lastResult = start.LastCompletionResult?.Payloads_.Select(v => new RawValue(v)).ToArray();
            static string? NonEmptyOrNull(string s) => string.IsNullOrEmpty(s) ? null : s;
            Info = new(
                Attempt: start.Attempt,
                ContinuedRunId: NonEmptyOrNull(start.ContinuedFromExecutionRunId),
                CronSchedule: NonEmptyOrNull(start.CronSchedule),
                ExecutionTimeout: start.WorkflowExecutionTimeout?.ToTimeSpan(),
                FirstExecutionRunId: start.FirstExecutionRunId,
                Headers: start.Headers,
                LastFailure: lastFailure,
                LastResult: lastResult,
                Namespace: details.Namespace,
                Parent: parent,
                Priority: start.Priority is { } p ? new(p) : Common.Priority.Default,
                RetryPolicy: start.RetryPolicy == null ? null : Common.RetryPolicy.FromProto(start.RetryPolicy),
                Root: root,
                RunId: act.RunId,
                RunTimeout: start.WorkflowRunTimeout?.ToTimeSpan(),
                StartTime: act.Timestamp.ToDateTime(),
                TaskQueue: details.TaskQueue,
                TaskTimeout: start.WorkflowTaskTimeout.ToTimeSpan(),
                WorkflowId: start.WorkflowId,
                WorkflowStartTime: start.StartTime.ToDateTime(),
                WorkflowType: start.WorkflowType);
            workflowStackTrace = details.WorkflowStackTrace;
            pendingTaskStackTraces = workflowStackTrace == WorkflowStackTrace.None ? null : new();
            logger = details.LoggerFactory.CreateLogger($"Temporalio.Workflow:{start.WorkflowType}");
            replaySafeLogger = new(logger);
            onTaskStarting = details.OnTaskStarting;
            onTaskCompleted = details.OnTaskCompleted;
            Random = new(details.Init.RandomnessSeed);
            TracingEventsEnabled = !details.DisableTracingEvents;
            workerLevelFailureExceptionTypes = details.WorkerLevelFailureExceptionTypes;
            disableEagerActivityExecution = details.DisableEagerActivityExecution;
            AssertValidLocalActivity = details.AssertValidLocalActivity;
            definitionOptions = new()
            {
                FailureExceptionTypes = Definition.FailureExceptionTypes,
                VersioningBehavior = Definition.VersioningBehavior ?? VersioningBehavior.Unspecified,
            };
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="WorkflowInstance"/> class.
        /// </summary>
        ~WorkflowInstance() => cancellationTokenSource.Dispose();

        /// <inheritdoc/>
        public override int MaximumConcurrencyLevel => 1;

        /// <summary>
        /// Gets a value indicating whether this workflow works with tracing events.
        /// </summary>
        public bool TracingEventsEnabled { get; private set; }

        /// <inheritdoc />
        public bool AllHandlersFinished => inProgressHandlers.Count == 0;

        /// <inheritdoc />
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        /// <inheritdoc />
        public bool ContinueAsNewSuggested { get; private set; }

        /// <inheritdoc />
        public string CurrentBuildId => CurrentDeploymentVersion?.BuildId ?? string.Empty;

        /// <inheritdoc />
        public WorkerDeploymentVersion? CurrentDeploymentVersion { get; private set; }

        /// <inheritdoc />
        public string CurrentDetails { get; set; } = string.Empty;

        /// <inheritdoc />
        public int CurrentHistoryLength { get; private set; }

        /// <inheritdoc />
        public int CurrentHistorySize { get; private set; }

        /// <inheritdoc />
        public WorkflowUpdateInfo? CurrentUpdateInfo => CurrentUpdateInfoLocal.Value;

        /// <inheritdoc />
        public WorkflowQueryDefinition? DynamicQuery
        {
            get => dynamicQuery;
            set
            {
                if (value != null && !value.Dynamic)
                {
                    throw new ArgumentException("Query is not dynamic");
                }
                dynamicQuery = value;
            }
        }

        /// <inheritdoc />
        public WorkflowSignalDefinition? DynamicSignal
        {
            get => dynamicSignal;
            set
            {
                if (value != null && !value.Dynamic)
                {
                    throw new ArgumentException("Signal is not dynamic");
                }
                dynamicSignal = value;
                if (value != null)
                {
                    // If it's not null, send _all_ buffered signals. We will copy all from
                    // buffered, clear buffered, and send all to apply signal
                    var signals = bufferedSignals.ToList();
                    bufferedSignals.Clear();
                    signals.ForEach(ApplySignalWorkflow);
                }
            }
        }

        /// <inheritdoc />
        public WorkflowUpdateDefinition? DynamicUpdate
        {
            get => dynamicUpdate;
            set
            {
                if (value != null && !value.Dynamic)
                {
                    throw new ArgumentException("Update is not dynamic");
                }
                dynamicUpdate = value;
            }
        }

        /// <inheritdoc />
        public WorkflowInfo Info { get; private init; }

        /// <inheritdoc />
        ///
        /// This is lazily created and should never be called outside of the scheduler
        public object Instance
        {
            get
            {
                // We create this lazily because we want the constructor in a workflow context
                instance ??= Definition.CreateWorkflowInstance(startArgs!.Value);
                // Dynamic options method needs to be invoked at this point, after initting the
                // workflow instance but before performing any activations.
                if (!dynamicOptionsGetterInvoked && Definition.DynamicOptionsGetter != null)
                {
                    dynamicOptionsGetterInvoked = true;
                    var dynOptions = Definition.DynamicOptionsGetter.Invoke(Instance);
                    if (dynOptions.FailureExceptionTypes != null)
                    {
                        definitionOptions.FailureExceptionTypes = dynOptions.FailureExceptionTypes;
                    }
                    if (dynOptions.VersioningBehavior != VersioningBehavior.Unspecified)
                    {
                        definitionOptions.VersioningBehavior = dynOptions.VersioningBehavior;
                    }
                }
                return instance;
            }
        }

        /// <inheritdoc />
        public bool IsReplaying { get; private set; }

        /// <inheritdoc />
        public ILogger Logger => replaySafeLogger;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IRawValue> Memo => memo.Value;

        /// <inheritdoc />
        public MetricMeter MetricMeter => metricMeter.Value;

        /// <inheritdoc />
        public IPayloadConverter PayloadConverter => payloadConverterWorkflowContext;

        /// <inheritdoc />
        public IDictionary<string, WorkflowQueryDefinition> Queries => mutableQueries.Value;

        /// <inheritdoc />
        public DeterministicRandom Random { get; private set; }

        /// <inheritdoc />
        public IDictionary<string, WorkflowSignalDefinition> Signals => mutableSignals.Value;

        /// <inheritdoc />
        public SearchAttributeCollection TypedSearchAttributes => typedSearchAttributes.Value;

        /// <inheritdoc />
        public IDictionary<string, WorkflowUpdateDefinition> Updates => mutableUpdates.Value;

        /// <inheritdoc />
        public DateTime UtcNow { get; private set; }

        /// <summary>
        /// Gets the workflow definition.
        /// </summary>
        internal WorkflowDefinition Definition { get; private init; }

        /// <summary>
        /// Gets the activity lookup function.
        /// </summary>
        internal Action<string> AssertValidLocalActivity { get; private init; }

        /// <inheritdoc/>
        public ContinueAsNewException CreateContinueAsNewException(
            string workflow, IReadOnlyCollection<object?> args, ContinueAsNewOptions? options) =>
            outbound.Value.CreateContinueAsNewException(new(
                Workflow: workflow,
                Args: args,
                Options: options,
                Headers: null));

        /// <inheritdoc/>
        public NexusClient CreateNexusClient(string service, NexusClientOptions options) =>
            new NexusClientImpl(this, service, options);

        /// <inheritdoc/>
        public NexusClient<TService> CreateNexusClient<TService>(NexusClientOptions options) =>
            new NexusClientImpl<TService>(this, options);

        /// <inheritdoc/>
        public Task DelayWithOptionsAsync(DelayOptions options) =>
            outbound.Value.DelayAsync(new(options));

        /// <inheritdoc/>
        public Task<TResult> ExecuteActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, ActivityOptions options) =>
            outbound.Value.ScheduleActivityAsync<TResult>(
                new(Activity: activity, Args: args, Options: options, Headers: null));

        /// <inheritdoc/>
        public Task<TResult> ExecuteLocalActivityAsync<TResult>(
            string activity, IReadOnlyCollection<object?> args, LocalActivityOptions options) =>
            outbound.Value.ScheduleLocalActivityAsync<TResult>(
                new(Activity: activity, Args: args, Options: options, Headers: null));

        /// <inheritdoc/>
        public ExternalWorkflowHandle<TWorkflow> GetExternalWorkflowHandle<TWorkflow>(
            string id, string? runId = null) =>
            new ExternalWorkflowHandleImpl<TWorkflow>(this, id, runId);

        /// <inheritdoc />
        public bool Patch(string patchId, bool deprecated)
        {
            // Use memoized result if present. If this is being deprecated, we can still use
            // memoized result and skip the command.
            if (patchesMemoized.TryGetValue(patchId, out var patched))
            {
                return patched;
            }
            patched = !IsReplaying || patchesNotified.Contains(patchId);
            patchesMemoized[patchId] = patched;
            if (patched)
            {
                AddCommand(new()
                {
                    SetPatchMarker = new() { PatchId = patchId, Deprecated = deprecated },
                });
            }
            return patched;
        }

        /// <inheritdoc/>
        public Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
            string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions options) =>
            outbound.Value.StartChildWorkflowAsync<TWorkflow, TResult>(
                new(Workflow: workflow, Args: args, Options: options, Headers: null));

        /// <inheritdoc />
        public void UpsertMemo(IReadOnlyCollection<MemoUpdate> updates)
        {
            if (updates.Count == 0)
            {
                throw new ArgumentException("At least one update required", nameof(updates));
            }
            // Validate and convert, then update map and issue command
            var upsertedMemo = new Memo();
            foreach (var update in updates)
            {
                if (upsertedMemo.Fields.ContainsKey(update.UntypedKey))
                {
                    throw new ArgumentException($"Multiple updates seen for key {update.UntypedKey}");
                }
                try
                {
                    // Unset is null
                    upsertedMemo.Fields[update.UntypedKey] = payloadConverterWorkflowContext.ToPayload(
                        update.HasValue ? update.UntypedValue : null);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Failed converting memo key {update.UntypedKey}", e);
                }
            }
            // Update map. We intentionally update separately after validation/conversion.
            foreach (var update in updates)
            {
                if (update.HasValue)
                {
                    // We set the raw payload knowing that if read again has to be converted again.
                    // This is intentional and clearer than having a form of RawValue with the
                    // already converted value. It can also make it clear to users that only what is
                    // converted is available (e.g. no private, unserialized fields/properties).
                    memo.Value[update.UntypedKey] = new RawValue(upsertedMemo.Fields[update.UntypedKey]);
                }
                else
                {
                    // We intentionally don't validate that an unset was for an existing key
                    memo.Value.Remove(update.UntypedKey);
                }
            }
            AddCommand(new() { ModifyWorkflowProperties = new() { UpsertedMemo = upsertedMemo } });
        }

        /// <inheritdoc/>
        public void UpsertTypedSearchAttributes(IReadOnlyCollection<SearchAttributeUpdate> updates)
        {
            if (updates.Count == 0)
            {
                throw new ArgumentException("At least one update required", nameof(updates));
            }
            // We update the map first then issue the command. We use the field to set but the
            // property to get so it is lazily created if needed.
            TypedSearchAttributes.ApplyUpdates(updates);
            AddCommand(new()
            {
                UpsertWorkflowSearchAttributes = new()
                {
                    SearchAttributes =
                    {
                        updates.Select(u =>
                            new KeyValuePair<string, Payload>(u.UntypedKey.Name, u.ToUpsertPayload())).
                                ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    },
                },
            });
        }

        /// <inheritdoc/>
        public Task<bool> WaitConditionWithOptionsAsync(WaitConditionOptions options)
        {
            var source = new TaskCompletionSource<object?>();
            var node = conditions.AddLast(Tuple.Create(options.ConditionCheck, source));
            var token = options.CancellationToken ?? CancellationToken;
            return QueueNewTaskAsync(async () =>
            {
                try
                {
                    using (token.Register(() => source.TrySetCanceled(token)))
                    {
                        // If there's no timeout, it'll never return false, so just wait
                        if (options.Timeout == null)
                        {
                            await source.Task.ConfigureAwait(true);
                            return true;
                        }
                        // Try a timeout that we cancel if never hit
                        using (var delayCancelSource = new CancellationTokenSource())
                        {
                            var completedTask = await Task.WhenAny(source.Task, DelayWithOptionsAsync(
                                new(
                                    delay: options.Timeout.GetValueOrDefault(),
                                    summary: options.TimeoutSummary,
                                    cancellationToken: delayCancelSource.Token))).ConfigureAwait(true);
                            // Do not timeout
                            if (completedTask == source.Task)
                            {
                                try
                                {
                                    await completedTask.ConfigureAwait(true);
                                    return true;
                                }
                                finally
                                {
                                    // Cancel delay timer
                                    delayCancelSource.Cancel();
                                }
                            }
                            // Timed out
                            return false;
                        }
                    }
                }
                finally
                {
                    conditions.Remove(node);
                }
            });
        }

        /// <inheritdoc/>
        public T WithTracingEventListenerDisabled<T>(Func<T> fn)
        {
            // Capture the existing value and restore it later. We recognize this is not thread safe
            // if other people have called this in other places, but we do not want to explicitly
            // prevent it or add a stack here and people should not be blocking in the function
            // anyways. This is an unsafe calls and docs on the user-facing portion adequately
            // explain the dangers.
            var prevEnabled = TracingEventsEnabled;
            TracingEventsEnabled = false;
            try
            {
                // Make sure no commands were added
                var origCmdCount = completion?.Successful?.Commands?.Count ?? 0;

                var result = fn();

                var newCmdCount = completion?.Successful?.Commands?.Count ?? 0;
                if (origCmdCount != newCmdCount)
                {
                    throw new InvalidOperationException(
                        "Function during tracing event listener disabling created workflow commands");
                }
                return result;
            }
            finally
            {
                TracingEventsEnabled = prevEnabled;
            }
        }

        /// <inheritdoc/>
        public WorkflowActivationCompletion Activate(WorkflowActivation act)
        {
            using (logger.BeginScope(Info.LoggerScope))
            {
                completion = new() { RunId = act.RunId, Successful = new() };
                currentActivationException = null;
                ContinueAsNewSuggested = act.ContinueAsNewSuggested;
                CurrentHistoryLength = checked((int)act.HistoryLength);
                CurrentHistorySize = checked((int)act.HistorySizeBytes);
                if (act.DeploymentVersionForCurrentTask != null)
                {
                    CurrentDeploymentVersion = WorkerDeploymentVersion.FromBridge(act.DeploymentVersionForCurrentTask);
                }
                IsReplaying = act.IsReplaying;
                UtcNow = act.Timestamp.ToDateTime();

                // If the workflow has not been initialized, we are in the first activation and we
                // need to set the modern-event-loop-logic flag
                if (!workflowInitialized)
                {
                    // If we're not replaying or we are replaying and the flag is already set, set
                    // to true and mark flag. Otherwise we leave false.
                    if (!IsReplaying ||
                        act.AvailableInternalFlags.Contains((uint)WorkflowLogicFlag.ApplyModernEventLoopLogic))
                    {
                        applyModernEventLoopLogic = true;
                        completion.Successful.UsedInternalFlags.Add((uint)WorkflowLogicFlag.ApplyModernEventLoopLogic);
                    }
                }

                // Starting callback
                onTaskStarting(this);

                // Run the event loop until yielded for each job
                Exception? failureException = null;
                try
                {
                    var previousContext = SynchronizationContext.Current;
                    try
                    {
                        // We must set the sync context to null so work isn't posted there
                        SynchronizationContext.SetSynchronizationContext(null);

                        // We only sort jobs in legacy event loop logic, modern relies on Core
                        List<WorkflowActivationJob> jobs;
                        if (applyModernEventLoopLogic)
                        {
                            jobs = act.Jobs.ToList();
                        }
                        else
                        {
                            jobs = act.Jobs.OrderBy(j =>
                            {
                                switch (j.VariantCase)
                                {
                                    case WorkflowActivationJob.VariantOneofCase.NotifyHasPatch:
                                    case WorkflowActivationJob.VariantOneofCase.UpdateRandomSeed:
                                        return 1;
                                    case WorkflowActivationJob.VariantOneofCase.SignalWorkflow:
                                    case WorkflowActivationJob.VariantOneofCase.DoUpdate:
                                        return 2;
                                    case WorkflowActivationJob.VariantOneofCase.InitializeWorkflow:
                                        return 3;
                                    default:
                                        return 4;
                                }
                            }).ToList();
                        }

                        // Apply each job
                        foreach (var job in jobs)
                        {
                            Apply(job, act);
                            // We only run the scheduler after each job in legacy event loop logic
                            if (!applyModernEventLoopLogic)
                            {
                                // Run scheduler once. Do not check conditions when patching or
                                // querying with legacy event loop logic.
                                var checkConditions = job.NotifyHasPatch == null && job.QueryWorkflow == null;
                                RunOnce(checkConditions);
                            }
                        }

                        // For modern event loop logic, we initialize here if not initialized
                        // already
                        if (applyModernEventLoopLogic && !workflowInitialized)
                        {
                            InitializeWorkflow();
                        }

                        // For modern event loop logic, we run the event loop only after applying
                        // everything, and we check conditions if there are any non-query jobs
                        if (applyModernEventLoopLogic)
                        {
                            var checkConditions = jobs.Any(j => j.VariantCase != WorkflowActivationJob.VariantOneofCase.QueryWorkflow);
                            RunOnce(checkConditions);
                        }

                        completion.Successful.VersioningBehavior =
                            (Temporalio.Api.Enums.V1.VersioningBehavior)definitionOptions.VersioningBehavior;
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(previousContext);
                    }
                }
                catch (Exception e)
                {
                    failureException = e;
                    logger.LogWarning(
                        e,
                        "Failed activation on workflow {WorkflowType} with ID {WorkflowId} and run ID {RunId}",
                        Info.WorkflowType,
                        Info.WorkflowId,
                        Info.RunId);
                    try
                    {
                        completion.Failed = new()
                        {
                            Failure_ = failureConverterWorkflowContext.ToFailure(
                                e, payloadConverterWorkflowContext),
                        };
                    }
                    catch (Exception inner)
                    {
                        logger.LogError(
                            inner,
                            "Failed converting activation exception on workflow with run ID {RunId}",
                            Info.RunId);
                        completion.Failed = new()
                        {
                            Failure_ = new() { Message = $"Failed converting activation exception: {inner}" },
                        };
                    }
                }

                // Maybe apply legacy workflow completion command reordering logic
                ApplyLegacyCompletionCommandReordering(act, completion, out var workflowCompleteNonFailure);

                // Log warnings if we have completed
                if (workflowCompleteNonFailure && !IsReplaying)
                {
                    inProgressHandlers.WarnIfAnyLeftOver(Info.WorkflowId, logger);
                }

                // Unset the completion
                var toReturn = completion;
                completion = null;

                // Completed callback
                onTaskCompleted(this, failureException);
                return toReturn;
            }
        }

        /// <summary>
        /// Set the given exception as the current activation exception. This has no effect if there
        /// is not a current activation or there is already an activation exception set.
        /// </summary>
        /// <param name="exc">Exception to set.</param>
        public void SetCurrentActivationException(Exception exc) => currentActivationException ??= exc;

        /// <inheritdoc/>
        public ISerializationContext.Activity? GetPendingActivitySerializationContext(uint seq)
        {
            activitiesPending.TryGetValue(seq, out var pending);
            return pending?.SerializationContext;
        }

        /// <inheritdoc/>
        public ISerializationContext.Workflow? GetPendingChildSerializationContext(uint seq)
        {
            childWorkflowsPending.TryGetValue(seq, out var pending);
            return pending?.SerializationContext;
        }

        /// <inheritdoc/>
        public ISerializationContext.Workflow? GetPendingExternalCancelSerializationContext(uint seq)
        {
            externalCancelsPending.TryGetValue(seq, out var pending);
            return pending?.SerializationContext;
        }

        /// <inheritdoc/>
        public ISerializationContext.Workflow? GetPendingExternalSignalSerializationContext(uint seq)
        {
            externalSignalsPending.TryGetValue(seq, out var pending);
            return pending?.SerializationContext;
        }

        /// <inheritdoc/>
        protected override IEnumerable<Task>? GetScheduledTasks() => scheduledTasks;

        /// <inheritdoc/>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // No benefit for executing inline, so just append to list if not there
            if (!taskWasPreviouslyQueued)
            {
                QueueTask(task);
            }
            return false;
        }

        /// <inheritdoc/>
        protected override void QueueTask(Task task)
        {
            // Only queue if not already done
            if (!scheduledTaskNodes.ContainsKey(task))
            {
                scheduledTaskNodes[task] = scheduledTasks.AddFirst(task);
            }
        }

        /// <inheritdoc/>
        protected override bool TryDequeue(Task task)
        {
            // Can't use remove overload that gets value at same time in oldest supported version
            if (scheduledTaskNodes.TryGetValue(task, out var node))
            {
                scheduledTaskNodes.Remove(task);
                scheduledTasks.Remove(node);
                return true;
            }
            return false;
        }

        private void RunOnce(bool checkConditions)
        {
            // Run as long as we have scheduled tasks
            while (scheduledTasks.Count > 0)
            {
                // Run all tasks until empty
                while (scheduledTasks.Count > 0)
                {
                    // Pop last
                    var task = scheduledTasks.Last!.Value;
                    scheduledTasks.RemoveLast();
                    scheduledTaskNodes.Remove(task);

                    // This should never return false
                    if (!TryExecuteTask(task))
                    {
                        logger.LogWarning("Task unexpectedly was unable to execute");
                    }
                    if (currentActivationException != null)
                    {
                        ExceptionDispatchInfo.Capture(currentActivationException).Throw();
                    }
                }

                // Check conditions. It would be nice if we could run this in the task scheduler
                // because then users could have access to the `Workflow` context in the condition
                // callback. However, this cannot be done because even just running one task in the
                // scheduler causes .NET to add more tasks to the scheduler. And you don't want to
                // "run until empty" with the condition, because conditions may need to be retried
                // based on each other. This sounds confusing but basically: can't run check
                // conditions in the task scheduler comfortably but still need to access the static
                // Workflow class, hence the context override.
                if (checkConditions && conditions.Count > 0)
                {
                    Workflow.OverrideContext.Value = this;
                    try
                    {
                        foreach (var condition in conditions)
                        {
                            // Check whether the condition evaluates to true
                            if (condition.Item1())
                            {
                                // Set condition as resolved
                                condition.Item2.TrySetResult(null);
                                // When applying modern event loop logic, we want to break after the
                                // first condition is resolved instead of checking/applying all
                                if (applyModernEventLoopLogic)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        Workflow.OverrideContext.Value = null;
                    }
                }
            }
        }

        private void AddCommand(WorkflowCommand cmd)
        {
            if (completion == null)
            {
                throw new InvalidOperationException("No completion available");
            }
            // We only add the command if we're still successful
            completion.Successful?.Commands.Add(cmd);
        }

#pragma warning disable CA2008 // We don't have to pass a scheduler, factory already implies one
#pragma warning disable VSTHRD003 // We know it's our own task we're waiting on
        private Task QueueNewTaskAsync(Func<Task> func)
        {
            // If we need a stack trace, wrap
            if (pendingTaskStackTraces is LinkedList<System.Diagnostics.StackTrace> stackTraces)
            {
                // Have to eagerly capture stack trace. We are unable to find an easy way to make
                // this lazy and still preserve the frames properly.
                var node = stackTraces.AddLast(
                    new System.Diagnostics.StackTrace(workflowStackTrace == WorkflowStackTrace.Normal));
                var origFunc = func;
                func = () =>
                {
                    var task = origFunc();
                    return task.ContinueWith(
                        _ =>
                        {
                            stackTraces.Remove(node);
                            return task;
                        },
                        this).Unwrap();
                };
            }
            return taskFactory.StartNew(func).Unwrap();
        }

        private Task<T> QueueNewTaskAsync<T>(Func<Task<T>> func)
        {
            // If we need a stack trace, wrap
            if (pendingTaskStackTraces is LinkedList<System.Diagnostics.StackTrace> stackTraces)
            {
                // Have to eagerly capture stack trace. We are unable to find an easy way to make
                // this lazy and still preserve the frames properly.
                var node = stackTraces.AddLast(
                    new System.Diagnostics.StackTrace(workflowStackTrace == WorkflowStackTrace.Normal));
                var origFunc = func;
                func = () =>
                {
                    var task = origFunc();
                    return task.ContinueWith(
                        _ =>
                        {
                            stackTraces.Remove(node);
                            return task;
                        },
                        this).Unwrap();
                };
            }
            return taskFactory.StartNew(func).Unwrap();
        }
#pragma warning restore VSTHRD003
#pragma warning restore CA2008

        private async Task RunTopLevelAsync(Func<Task> func)
        {
            try
            {
                try
                {
                    await func().ConfigureAwait(true);
                }
                catch (ContinueAsNewException e)
                {
                    logger.LogDebug("Workflow requested continue as new with run ID {RunId}", Info.RunId);
                    var cmd = new ContinueAsNewWorkflowExecution()
                    {
                        WorkflowType = e.Input.Workflow,
                        TaskQueue = e.Input.Options?.TaskQueue ?? string.Empty,
                        Arguments = { payloadConverterWorkflowContext.ToPayloads(e.Input.Args) },
                        RetryPolicy = e.Input.Options?.RetryPolicy?.ToProto(),
                    };
                    if (e.Input.Options?.RunTimeout is TimeSpan runTimeout)
                    {
                        cmd.WorkflowRunTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(runTimeout);
                    }
                    if (e.Input.Options?.TaskTimeout is TimeSpan taskTimeout)
                    {
                        cmd.WorkflowTaskTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(taskTimeout);
                    }
                    if (e.Input.Options?.Memo is IReadOnlyDictionary<string, object> memo)
                    {
                        cmd.Memo.Add(memo.ToDictionary(
                            kvp => kvp.Key, kvp => payloadConverterWorkflowContext.ToPayload(kvp.Value)));
                    }
                    if (e.Input.Options?.TypedSearchAttributes is SearchAttributeCollection attrs)
                    {
                        cmd.SearchAttributes.Add(attrs.ToProto().IndexedFields);
                    }
                    if (e.Input.Headers is IDictionary<string, Payload> headers)
                    {
                        cmd.Headers.Add(headers);
                    }
#pragma warning disable CS0618
                    if (e.Input.Options?.VersioningIntent is { } vi)
                    {
                        cmd.VersioningIntent = (Bridge.Api.Common.VersioningIntent)(int)vi;
                    }
#pragma warning restore CS0618
                    AddCommand(new() { ContinueAsNewWorkflowExecution = cmd });
                }
                catch (Exception e) when (
                    CancellationToken.IsCancellationRequested && TemporalException.IsCanceledException(e))
                {
                    // If cancel was ever requested and this is a cancellation or an activity/child
                    // cancellation, we add a cancel command. Technically this means that a
                    // swallowed cancel followed by, say, an activity cancel later on will show the
                    // workflow as cancelled. But this is a Temporal limitation in that cancellation
                    // is a state not an event.
                    logger.LogDebug(e, "Workflow raised cancel with run ID {RunId}", Info.RunId);
                    AddCommand(new() { CancelWorkflowExecution = new() });
                }
                catch (Exception e) when (IsWorkflowFailureException(e))
                {
                    // We let this failure conversion throw if it cannot convert the failure
                    logger.LogDebug(e, "Workflow raised failure with run ID {RunId}", Info.RunId);
                    var failure = failureConverterWorkflowContext.ToFailure(
                        e, payloadConverterWorkflowContext);
                    AddCommand(new() { FailWorkflowExecution = new() { Failure = failure } });
                }
            }
            catch (Exception e)
            {
                logger.LogDebug(e, "Workflow raised unexpected failure with run ID {RunId}", Info.RunId);
                // All exceptions this far fail the task
                currentActivationException = e;
            }
        }

        private bool IsWorkflowFailureException(Exception e) =>
            // Failure exceptions fail the workflow. We also allow non-internally-caught
            // cancellation exceptions fail the workflow because it's clearer when users are
            // reusing cancellation tokens if the workflow fails.
            e is FailureException ||
                e is OperationCanceledException ||
                definitionOptions.FailureExceptionTypes?.Any(t => t.IsAssignableFrom(e.GetType())) == true ||
                workerLevelFailureExceptionTypes?.Any(t => t.IsAssignableFrom(e.GetType())) == true;

        private void Apply(WorkflowActivationJob job, WorkflowActivation act)
        {
            switch (job.VariantCase)
            {
                case WorkflowActivationJob.VariantOneofCase.CancelWorkflow:
                    // TODO(cretz): Do we care about "details" on the object?
                    ApplyCancelWorkflow();
                    break;
                case WorkflowActivationJob.VariantOneofCase.DoUpdate:
                    ApplyDoUpdate(job.DoUpdate);
                    break;
                case WorkflowActivationJob.VariantOneofCase.FireTimer:
                    ApplyFireTimer(job.FireTimer);
                    break;
                case WorkflowActivationJob.VariantOneofCase.NotifyHasPatch:
                    ApplyNotifyHasPatch(job.NotifyHasPatch);
                    break;
                case WorkflowActivationJob.VariantOneofCase.QueryWorkflow:
                    ApplyQueryWorkflow(job.QueryWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.RemoveFromCache:
                    // Ignore, handled outside the instance
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveActivity:
                    ApplyResolveActivity(job.ResolveActivity);
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecution:
                    ApplyResolveChildWorkflowExecution(job.ResolveChildWorkflowExecution);
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveChildWorkflowExecutionStart:
                    ApplyResolveChildWorkflowExecutionStart(job.ResolveChildWorkflowExecutionStart);
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveNexusOperation:
                    ApplyResolveNexusOperation(job.ResolveNexusOperation);
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveNexusOperationStart:
                    ApplyResolveNexusOperationStart(job.ResolveNexusOperationStart);
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveRequestCancelExternalWorkflow:
                    ApplyResolveRequestCancelExternalWorkflow(job.ResolveRequestCancelExternalWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveSignalExternalWorkflow:
                    ApplyResolveSignalExternalWorkflow(job.ResolveSignalExternalWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.SignalWorkflow:
                    ApplySignalWorkflow(job.SignalWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.InitializeWorkflow:
                    // We only initialize the workflow at job time on legacy event loop logic
                    if (!applyModernEventLoopLogic)
                    {
                        InitializeWorkflow();
                    }
                    break;
                case WorkflowActivationJob.VariantOneofCase.UpdateRandomSeed:
                    ApplyUpdateRandomSeed(job.UpdateRandomSeed);
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognized job: {job.VariantCase}");
            }
        }

        private void ApplyCancelWorkflow() => cancellationTokenSource.Cancel();

        private void ApplyDoUpdate(DoUpdate update)
        {
            // Queue it up so it can run in workflow environment
            _ = QueueNewTaskAsync(() =>
            {
                // Make sure we have loaded the instance which may invoke the constructor thereby
                // letting the constructor register update handlers at runtime
                var ignored = Instance;

                // Set the current update for the life of this task
                var updateInfo = new WorkflowUpdateInfo(Id: update.Id, Name: update.Name);
                CurrentUpdateInfoLocal.Value = updateInfo;

                // Put the entire update in the log scope
                using (logger.BeginScope(updateInfo.CreateLoggerScope()))
                {
                    return ApplyDoUpdateAsync(update);
                }
            });
        }

        private Task ApplyDoUpdateAsync(DoUpdate update)
        {
            // Find update definition or reject
            var updates = mutableUpdates.IsValueCreated ? mutableUpdates.Value : Definition.Updates;
            if (!updates.TryGetValue(update.Name, out var updateDefn))
            {
                // Do not fall back onto dynamic update if using the reserved prefix
                if (!update.Name.StartsWith(TemporalRuntime.ReservedNamePrefix))
                {
                    updateDefn = DynamicUpdate;
                }
                if (updateDefn == null)
                {
                    var knownUpdates = updates.Keys.OrderBy(k => k);
                    var failure = new InvalidOperationException(
                        $"Update handler for {update.Name} expected but not found, " +
                        $"known updates: [{string.Join(" ", knownUpdates)}]");
                    AddCommand(new()
                    {
                        UpdateResponse = new()
                        {
                            ProtocolInstanceId = update.ProtocolInstanceId,
                            Rejected = failureConverterWorkflowContext.ToFailure(
                                failure, payloadConverterWorkflowContext),
                        },
                    });
                    return Task.CompletedTask;
                }
            }

            // May be loaded inside validate after validator, maybe not
            object?[]? argsForUpdate = null;
            // We define this up here because there are multiple places it's called below
            object?[] DecodeUpdateArgs() => DecodeArgs(
                method: updateDefn.Method ?? updateDefn.Delegate!.Method,
                payloads: update.Input,
                itemName: $"Update {update.Name}",
                dynamic: updateDefn.Dynamic,
                dynamicArgPrepend: update.Name);

            // Do validation. Whether or not this runs a validator, this should accept/reject.
            try
            {
                if (update.RunValidator)
                {
                    // We only call the validation interceptor if a validator is present. We are
                    // not allowed to share the arguments. We do not share the arguments (i.e.
                    // we re-convert) to prevent a user from mistakenly mutating an argument in
                    // the validator. We call the interceptor only if a validator is present to
                    // match other SDKs where doubly converting arguments here unnecessarily
                    // (because of the no-reuse-argument rule above) causes performance issues
                    // for them (even if they don't much for us).
                    if (updateDefn.ValidatorMethod != null || updateDefn.ValidatorDelegate != null)
                    {
                        // Capture command count so we can ensure it is unchanged after call
                        var origCmdCount = completion?.Successful?.Commands?.Count ?? 0;
                        inbound.Value.ValidateUpdate(new(
                            Id: update.Id,
                            Update: update.Name,
                            Definition: updateDefn,
                            Args: DecodeUpdateArgs(),
                            Headers: update.Headers));
                        // If the command count changed, we need to issue a task failure
                        var newCmdCount = completion?.Successful?.Commands?.Count ?? 0;
                        if (origCmdCount != newCmdCount)
                        {
                            currentActivationException = new InvalidOperationException(
                                $"Update validator for {update.Name} created workflow commands");
                            return Task.CompletedTask;
                        }
                    }

                    // We want to try to decode args here _inside_ the validator rejection
                    // try/catch so we can prevent acceptance on invalid args
                    argsForUpdate = DecodeUpdateArgs();
                }

                // Send accepted
                AddCommand(new()
                {
                    UpdateResponse = new()
                    {
                        ProtocolInstanceId = update.ProtocolInstanceId,
                        Accepted = new(),
                    },
                });
            }
            catch (Exception e)
            {
                // Send rejected
                AddCommand(new()
                {
                    UpdateResponse = new()
                    {
                        ProtocolInstanceId = update.ProtocolInstanceId,
                        Rejected = failureConverterWorkflowContext.ToFailure(
                            e, payloadConverterWorkflowContext),
                    },
                });
                return Task.CompletedTask;
            }

            // Issue actual update. We are using ContinueWith instead of await here so that user
            // code can run immediately. But the user code _or_ the task could fail so we need
            // to reject in both cases.
            try
            {
                // If the args were not already decoded because we didn't run the validation
                // step, decode them here
                argsForUpdate ??= DecodeUpdateArgs();

                var task = inbound.Value.HandleUpdateAsync(new(
                    Id: update.Id,
                    Update: update.Name,
                    Definition: updateDefn,
                    Args: argsForUpdate,
                    Headers: update.Headers));
                var inProgress = inProgressHandlers.AddLast(new Handlers.Handler(
                    update.Name, update.Id, updateDefn.UnfinishedPolicy));
                return task.ContinueWith(
                    _ =>
                    {
                        try
                        {
                            inProgressHandlers.Remove(inProgress);
                            // If workflow failure exception, it's an update failure. If it's some
                            // other exception, it's a task failure. Otherwise it's a success.
                            var exc = task.Exception?.InnerExceptions?.SingleOrDefault();
                            // There are .NET cases where cancellation occurs but is not considered
                            // an exception. We are going to make it an exception. Unfortunately
                            // there is no easy way to make it include the outer stack trace at this
                            // time.
                            if (exc == null && task.IsCanceled)
                            {
                                exc = new TaskCanceledException();
                            }
                            if (exc != null && IsWorkflowFailureException(exc))
                            {
                                AddCommand(new()
                                {
                                    UpdateResponse = new()
                                    {
                                        ProtocolInstanceId = update.ProtocolInstanceId,
                                        Rejected = failureConverterWorkflowContext.ToFailure(
                                            exc, payloadConverterWorkflowContext),
                                    },
                                });
                            }
                            else if (task.Exception is { } taskExc)
                            {
                                // Fails the task
                                currentActivationException =
                                    taskExc.InnerExceptions.SingleOrDefault() ?? taskExc;
                            }
                            else
                            {
                                // Success, have to use reflection to extract value if it's a Task<>
                                try
                                {
                                    var taskType = task.GetType();
                                    var result = taskType.IsGenericType ?
                                        taskType.GetProperty("Result")!.GetValue(task) : ValueTuple.Create();
                                    AddCommand(new()
                                    {
                                        UpdateResponse = new()
                                        {
                                            ProtocolInstanceId = update.ProtocolInstanceId,
                                            Completed = payloadConverterWorkflowContext.ToPayload(result),
                                        },
                                    });
                                }
                                catch (Exception e) when (IsWorkflowFailureException(e))
                                {
                                    // Payload conversion can fail with a fail-update exception
                                    // instead of a fail-task exception. Any failure here does
                                    // bubble to outer catch as task failure.
                                    AddCommand(new()
                                    {
                                        UpdateResponse = new()
                                        {
                                            ProtocolInstanceId = update.ProtocolInstanceId,
                                            Rejected = failureConverterWorkflowContext.ToFailure(
                                                e, payloadConverterWorkflowContext),
                                        },
                                    });
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            currentActivationException = e;
                        }
                        return Task.CompletedTask;
                    },
                    this).Unwrap();
            }
            catch (FailureException e)
            {
                AddCommand(new()
                {
                    UpdateResponse = new()
                    {
                        ProtocolInstanceId = update.ProtocolInstanceId,
                        Rejected = failureConverterWorkflowContext.ToFailure(
                            e, payloadConverterWorkflowContext),
                    },
                });
                return Task.CompletedTask;
            }
        }

        private void ApplyFireTimer(FireTimer fireTimer)
        {
            if (timersPending.TryGetValue(fireTimer.Seq, out var source))
            {
                timersPending.Remove(fireTimer.Seq);
                source.TrySetResult(null);
            }
        }

        private void ApplyNotifyHasPatch(NotifyHasPatch notify) =>
            patchesNotified.Add(notify.PatchId);

        private void ApplyQueryWorkflow(QueryWorkflow query)
        {
            // Queue it up so it can run in workflow environment
            _ = QueueNewTaskAsync(() =>
            {
                // Make sure we have loaded the instance which may invoke the constructor thereby
                // letting the constructor register query handlers at runtime
                var ignored = Instance;

                var origCmdCount = completion?.Successful?.Commands?.Count;
                try
                {
                    WorkflowQueryDefinition? queryDefn;
                    object? resultObj;

                    if (query.QueryType == "__stack_trace")
                    {
                        // Use raw value built from default converter because we don't want to use
                        // user-conversion
                        resultObj = new RawValue(DataConverter.Default.PayloadConverter.ToPayload(
                            GetStackTrace()));
                    }
                    else if (query.QueryType == "__temporal_workflow_metadata")
                    {
                        // Use raw value built from default converter because we don't want to use
                        // user-conversion
                        resultObj = new RawValue(DataConverter.Default.PayloadConverter.ToPayload(
                            GetWorkflowMetadata()));
                    }
                    else
                    {
                        // Find definition or fail
                        var queries = mutableQueries.IsValueCreated ? mutableQueries.Value : Definition.Queries;
                        if (!queries.TryGetValue(query.QueryType, out queryDefn))
                        {
                            // Do not fall back onto dynamic query if using the reserved prefix
                            if (!query.QueryType.StartsWith(TemporalRuntime.ReservedNamePrefix))
                            {
                                queryDefn = DynamicQuery;
                            }
                            if (queryDefn == null)
                            {
                                var knownQueries = queries.Keys.OrderBy(k => k);
                                throw new InvalidOperationException(
                                    $"Query handler for {query.QueryType} expected but not found, " +
                                    $"known queries: [{string.Join(" ", knownQueries)}]");
                            }
                        }
                        resultObj = inbound.Value.HandleQuery(new(
                            Id: query.QueryId,
                            Query: query.QueryType,
                            Definition: queryDefn,
                            Args: DecodeArgs(
                                method: queryDefn.Method ?? queryDefn.Delegate!.Method,
                                payloads: query.Arguments,
                                itemName: $"Query {query.QueryType}",
                                dynamic: queryDefn.Dynamic,
                                dynamicArgPrepend: query.QueryType),
                            Headers: query.Headers));
                    }
                    AddCommand(new()
                    {
                        RespondToQuery = new()
                        {
                            QueryId = query.QueryId,
                            Succeeded = new() { Response = payloadConverterWorkflowContext.ToPayload(resultObj) },
                        },
                    });
                }
                catch (Exception e)
                {
                    AddCommand(new()
                    {
                        RespondToQuery = new()
                        {
                            QueryId = query.QueryId,
                            Failed = failureConverterWorkflowContext.ToFailure(
                                e, payloadConverterWorkflowContext),
                        },
                    });
                    return Task.CompletedTask;
                }
                // Check for commands but don't include null counts in check since Successful is
                // unset by other completion failures
                var newCmdCount = completion?.Successful?.Commands?.Count;
                if (origCmdCount != null && newCmdCount != null && origCmdCount! + 1 != newCmdCount)
                {
                    currentActivationException = new InvalidOperationException(
                        $"Query handler for {query.QueryType} created workflow commands");
                }
                return Task.CompletedTask;
            });
        }

        private void ApplyResolveActivity(ResolveActivity resolve)
        {
            if (!activitiesPending.TryGetValue(resolve.Seq, out var pending))
            {
                throw new InvalidOperationException(
                    $"Failed finding activity for sequence {resolve.Seq}");
            }
            pending.CompletionSource.TrySetResult(resolve.Result);
        }

        private void ApplyResolveChildWorkflowExecution(ResolveChildWorkflowExecution resolve)
        {
            if (!childWorkflowsPending.TryGetValue(resolve.Seq, out var pending))
            {
                throw new InvalidOperationException(
                    $"Failed finding child for sequence {resolve.Seq}");
            }
            pending.ResultCompletionSource.TrySetResult(resolve.Result);
        }

        private void ApplyResolveChildWorkflowExecutionStart(
            ResolveChildWorkflowExecutionStart resolve)
        {
            if (!childWorkflowsPending.TryGetValue(resolve.Seq, out var pending))
            {
                throw new InvalidOperationException(
                    $"Failed finding child for sequence {resolve.Seq}");
            }
            pending.StartCompletionSource.TrySetResult(resolve);
        }

        private void ApplyResolveNexusOperation(ResolveNexusOperation resolve)
        {
            if (!nexusOperationsPending.TryGetValue(resolve.Seq, out var pending))
            {
                throw new InvalidOperationException(
                    $"Failed finding Nexus operation for sequence {resolve.Seq}");
            }
            pending.ResultCompletionSource.TrySetResult(resolve.Result);
        }

        private void ApplyResolveNexusOperationStart(ResolveNexusOperationStart resolve)
        {
            if (!nexusOperationsPending.TryGetValue(resolve.Seq, out var pending))
            {
                throw new InvalidOperationException(
                    $"Failed finding Nexus operation for sequence {resolve.Seq}");
            }
            pending.StartCompletionSource.TrySetResult(resolve);
        }

        private void ApplyResolveRequestCancelExternalWorkflow(
            ResolveRequestCancelExternalWorkflow resolve)
        {
            if (!externalCancelsPending.TryGetValue(resolve.Seq, out var pending))
            {
                throw new InvalidOperationException(
                    $"Failed finding external cancel for sequence {resolve.Seq}");
            }
            pending.CompletionSource.TrySetResult(resolve);
        }

        private void ApplyResolveSignalExternalWorkflow(ResolveSignalExternalWorkflow resolve)
        {
            if (!externalSignalsPending.TryGetValue(resolve.Seq, out var pending))
            {
                throw new InvalidOperationException(
                    $"Failed finding external signal for sequence {resolve.Seq}");
            }
            pending.CompletionSource.TrySetResult(resolve);
        }

        private void ApplySignalWorkflow(SignalWorkflow signal)
        {
            // Find applicable definition or buffer
            var signals = mutableSignals.IsValueCreated ? mutableSignals.Value : Definition.Signals;
            if (!signals.TryGetValue(signal.SignalName, out var signalDefn))
            {
                // Do not fall back onto dynamic signal if using the reserved prefix
                if (!signal.SignalName.StartsWith(TemporalRuntime.ReservedNamePrefix))
                {
                    signalDefn = DynamicSignal;
                }
                if (signalDefn == null)
                {
                    // No definition found, buffer
                    bufferedSignals.Add(signal);
                    return;
                }
            }

            // Run the handler as a top-level function
            _ = QueueNewTaskAsync(() => RunTopLevelAsync(async () =>
            {
                // Drop the signal if we cannot decode the arguments
                object?[] args;
                try
                {
                    args = DecodeArgs(
                        method: signalDefn.Method ?? signalDefn.Delegate!.Method,
                        payloads: signal.Input,
                        itemName: $"Signal {signal.SignalName}",
                        dynamic: signalDefn.Dynamic,
                        dynamicArgPrepend: signal.SignalName);
                }
                catch (Exception e)
                {
                    logger.LogError(
                        e,
                        "Failed decoding signal args for {SignalName}, dropping the signal",
                        signal.SignalName);
                    return;
                }

                // Handle signal
                var inProgress = inProgressHandlers.AddLast(new Handlers.Handler(
                    signal.SignalName, null, signalDefn.UnfinishedPolicy));
                try
                {
                    await inbound.Value.HandleSignalAsync(new(
                        Signal: signal.SignalName,
                        Definition: signalDefn,
                        Args: args,
                        Headers: signal.Headers)).ConfigureAwait(true);
                }
                finally
                {
                    inProgressHandlers.Remove(inProgress);
                }
            }));
        }

        private void ApplyUpdateRandomSeed(UpdateRandomSeed update) =>
            Random = new(update.RandomnessSeed);

        private void InitializeWorkflow()
        {
            if (workflowInitialized)
            {
                throw new InvalidOperationException("Workflow unexpectedly initialized");
            }
            workflowInitialized = true;
            _ = QueueNewTaskAsync(() => RunTopLevelAsync(async () =>
            {
                var input = new ExecuteWorkflowInput(
                    Instance: Instance,
                    RunMethod: Definition.RunMethod,
                    Args: startArgs!.Value);
                // We no longer need start args after this point, so we are unsetting them
                startArgs = null;
                var resultObj = await inbound.Value.ExecuteWorkflowAsync(input).ConfigureAwait(true);
                var result = payloadConverterWorkflowContext.ToPayload(resultObj);
                AddCommand(new() { CompleteWorkflowExecution = new() { Result = result } });
            }));
        }

        private void OnQueryDefinitionAdded(string name, WorkflowQueryDefinition defn)
        {
            if (defn.Dynamic)
            {
                throw new ArgumentException($"Cannot set dynamic query with other queries");
            }
            if (name != defn.Name)
            {
                throw new ArgumentException($"Query name {name} doesn't match definition name {defn.Name}");
            }
        }

        private void OnSignalDefinitionAdded(string name, WorkflowSignalDefinition defn)
        {
            if (defn.Dynamic)
            {
                throw new ArgumentException($"Cannot set dynamic signal with other signals");
            }
            if (name != defn.Name)
            {
                throw new ArgumentException($"Signal name {name} doesn't match definition name {defn.Name}");
            }
            // Send all buffered signals that apply to this one. We are going to collect these by
            // iterating list in reverse matching predicate while removing, then iterating in
            // reverse
            var buffered = new List<SignalWorkflow>();
            for (var i = bufferedSignals.Count - 1; i >= 0; i--)
            {
                if (bufferedSignals[i].SignalName == name)
                {
                    buffered.Add(bufferedSignals[i]);
                    bufferedSignals.RemoveAt(i);
                }
            }
            buffered.Reverse();
            buffered.ForEach(ApplySignalWorkflow);
        }

        private void OnUpdateDefinitionAdded(string name, WorkflowUpdateDefinition defn)
        {
            if (defn.Dynamic)
            {
                throw new ArgumentException($"Cannot set dynamic update with other updates");
            }
            if (name != defn.Name)
            {
                throw new ArgumentException($"Update name {name} doesn't match definition name {defn.Name}");
            }
        }

        private object?[] DecodeArgs(
            MethodInfo method,
            RepeatedField<Payload> payloads,
            string itemName,
            bool dynamic,
            string? dynamicArgPrepend = null)
        {
            // If the method is dynamic, a single vararg IRawValue parameter is guaranteed at the
            // end
            if (dynamic)
            {
                var raw = payloads.Select(p => new RawValue(p)).ToArray();
                if (dynamicArgPrepend != null)
                {
                    return new object?[] { dynamicArgPrepend, raw };
                }
                return new object?[] { raw };
            }

            var paramInfos = method.GetParameters();
            if (payloads.Count < paramInfos.Length && !paramInfos[payloads.Count].HasDefaultValue)
            {
                throw new InvalidOperationException(
                    $"{itemName} given {payloads.Count} parameter(s)," +
                    " but more than that are required by the signature");
            }
            // Zip the params and input and then decode each. It is intentional that we discard
            // extra input arguments that the signature doesn't accept.
            var paramVals = new List<object?>(paramInfos.Length);
            try
            {
                paramVals.AddRange(
                    payloads.Zip(paramInfos, (payload, paramInfo) =>
                        payloadConverterWorkflowContext.ToValue(payload, paramInfo.ParameterType)));
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"{itemName} had failure decoding parameters", e);
            }
            // Append default parameters if needed
            for (var i = payloads.Count; i < paramInfos.Length; i++)
            {
                paramVals.Add(paramInfos[i].DefaultValue);
            }
            return paramVals.ToArray();
        }

        private string GetStackTrace()
        {
            if (pendingTaskStackTraces is not LinkedList<System.Diagnostics.StackTrace> stackTraces)
            {
                throw new InvalidOperationException(
                    "Workflow stack traces are not enabled on this worker");
            }
            return string.Join("\n\n", stackTraces.Select(s =>
            {
                IEnumerable<string> lines = s.ToString().Split(
                    Newlines, StringSplitOptions.RemoveEmptyEntries);

                // Trim off leading lines until first non-worker line
                lines = lines.SkipWhile(line => line.Contains(" at Temporalio.Worker."));

                // Trim off trailing lines until first non-worker,
                // non-system-runtime/threading/reflection line
                lines = lines.Reverse().SkipWhile(line =>
                    line.Contains(" at System.Reflection.") ||
                    line.Contains(" at System.Runtime.") ||
                    line.Contains(" at System.RuntimeMethodHandle.") ||
                    line.Contains(" at System.Threading.") ||
                    line.Contains(" at Temporalio.Worker.")).Reverse();
                return string.Join("\n", lines);
            }).Where(s => !string.IsNullOrEmpty(s)).Select(s => $"Task waiting at:\n{s}"));
        }

        private Api.Sdk.V1.WorkflowMetadata GetWorkflowMetadata()
        {
            var defn = new Api.Sdk.V1.WorkflowDefinition() { Type = Info.WorkflowType };
            if (DynamicQuery is { } dynQuery)
            {
                defn.QueryDefinitions.Add(
                    new Api.Sdk.V1.WorkflowInteractionDefinition() { Description = dynQuery.Description });
            }
            defn.QueryDefinitions.AddRange(Queries.Values.Select(query =>
                new Api.Sdk.V1.WorkflowInteractionDefinition()
                {
                    Name = query.Name ?? string.Empty,
                    Description = query.Description ?? string.Empty,
                }).OrderBy(q => q.Name));
            if (DynamicSignal is { } dynSignal)
            {
                defn.SignalDefinitions.Add(
                    new Api.Sdk.V1.WorkflowInteractionDefinition() { Description = dynSignal.Description });
            }
            defn.SignalDefinitions.AddRange(Signals.Values.Select(query =>
                new Api.Sdk.V1.WorkflowInteractionDefinition()
                {
                    Name = query.Name ?? string.Empty,
                    Description = query.Description ?? string.Empty,
                }).OrderBy(q => q.Name));
            if (DynamicUpdate is { } dynUpdate)
            {
                defn.UpdateDefinitions.Add(
                    new Api.Sdk.V1.WorkflowInteractionDefinition() { Description = dynUpdate.Description });
            }
            defn.UpdateDefinitions.AddRange(Updates.Values.Select(query =>
                new Api.Sdk.V1.WorkflowInteractionDefinition()
                {
                    Name = query.Name ?? string.Empty,
                    Description = query.Description ?? string.Empty,
                }).OrderBy(q => q.Name));
            return new() { Definition = defn, CurrentDetails = CurrentDetails };
        }

        private void ApplyLegacyCompletionCommandReordering(
            WorkflowActivation act,
            WorkflowActivationCompletion completion,
            out bool workflowCompleteNonFailure)
        {
            // Find the index of the last completion command
            var lastCompletionCommandIndex = -1;
            workflowCompleteNonFailure = false;
            if (completion.Successful != null)
            {
                // Iterate in reverse
                for (var i = completion.Successful.Commands.Count - 1; i >= 0; i--)
                {
                    var cmd = completion.Successful.Commands[i];
                    // Set completion index if the command is a completion
                    if (cmd.CancelWorkflowExecution != null ||
                        cmd.CompleteWorkflowExecution != null ||
                        cmd.ContinueAsNewWorkflowExecution != null ||
                        cmd.FailWorkflowExecution != null)
                    {
                        // Only set this if not already set since we want the _last_ one and this
                        // iterates in reverse
                        if (lastCompletionCommandIndex == -1)
                        {
                            lastCompletionCommandIndex = i;
                        }
                        // Always override this bool because we want whether the _first_ completion
                        // is a non-failure, not the last and this iterates in reverse
                        workflowCompleteNonFailure = cmd.FailWorkflowExecution == null;
                    }
                }
            }

            // In a previous version of .NET SDK, if this was a successful activation completion
            // with a completion command not at the end, we'd reorder it to move at the end.
            // However, this logic has now moved to core and become more robust. Therefore, we only
            // apply this logic if we're replaying and flag is present so that workflows/histories
            // that were created after this .NET flag but before the core flag still work.
            if (completion.Successful == null ||
                lastCompletionCommandIndex == -1 ||
                lastCompletionCommandIndex == completion.Successful.Commands.Count - 1 ||
                !IsReplaying ||
                !act.AvailableInternalFlags.Contains((uint)WorkflowLogicFlag.ReorderWorkflowCompletion))
            {
                return;
            }

            // Now we know that we're replaying w/ the flag set and the completion in the wrong
            // spot, so set the SDK flag and move it
            completion.Successful.UsedInternalFlags.Add((uint)WorkflowLogicFlag.ReorderWorkflowCompletion);
            var compCmd = completion.Successful.Commands[lastCompletionCommandIndex];
            completion.Successful.Commands.RemoveAt(lastCompletionCommandIndex);
            completion.Successful.Commands.Insert(completion.Successful.Commands.Count, compCmd);
        }

        /// <summary>
        /// Workflow inbound implementation.
        /// </summary>
        internal class InboundImpl : WorkflowInboundInterceptor
        {
            private readonly WorkflowInstance instance;

            /// <summary>
            /// Initializes a new instance of the <see cref="InboundImpl"/> class.
            /// </summary>
            /// <param name="instance">Workflow instance.</param>
            public InboundImpl(WorkflowInstance instance) => this.instance = instance;

            /// <summary>
            /// Gets the outbound implementation.
            /// </summary>
            internal WorkflowOutboundInterceptor? Outbound { get; private set; }

            /// <inheritdoc />
            public override void Init(WorkflowOutboundInterceptor outbound) => Outbound = outbound;

            /// <inheritdoc />
            public override async Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
            {
                // Have to unwrap and re-throw target invocation exception if present
                Task resultTask;
                try
                {
                    resultTask = (Task)input.RunMethod.Invoke(input.Instance, input.Args)!;
                    await resultTask.ConfigureAwait(true);
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                    // Unreachable
                    return null;
                }
                var resultTaskType = resultTask.GetType();
                // We have to use reflection to extract value if it's a Task<>
                if (resultTaskType.IsGenericType)
                {
                    return resultTaskType.GetProperty("Result")!.GetValue(resultTask);
                }
                return null;
            }

            /// <inheritdoc />
            public override async Task HandleSignalAsync(HandleSignalInput input)
            {
                try
                {
                    Task task;
                    if (input.Definition.Method is MethodInfo method)
                    {
                        task = (Task)method.Invoke(instance.Instance, input.Args)!;
                    }
                    else
                    {
                        task = (Task)input.Definition.Delegate!.DynamicInvoke(input.Args)!;
                    }
                    await task.ConfigureAwait(true);
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                }
            }

            /// <inheritdoc />
            public override object? HandleQuery(HandleQueryInput input)
            {
                try
                {
                    if (input.Definition.Method is MethodInfo method)
                    {
                        return method.Invoke(instance.Instance, input.Args);
                    }
                    return input.Definition.Delegate!.DynamicInvoke(input.Args);
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                    // Unreachable
                    return null;
                }
            }

            /// <inheritdoc />
            public override void ValidateUpdate(HandleUpdateInput input)
            {
                try
                {
                    if (input.Definition.ValidatorMethod is MethodInfo method)
                    {
                        method.Invoke(instance.Instance, input.Args);
                    }
                    else if (input.Definition.ValidatorDelegate is Delegate del)
                    {
                        del.DynamicInvoke(input.Args);
                    }
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                }
            }

            /// <inheritdoc />
            public override async Task<object?> HandleUpdateAsync(HandleUpdateInput input)
            {
                // Have to unwrap and re-throw target invocation exception if present
                Task resultTask;
                try
                {
                    if (input.Definition.Method is MethodInfo method)
                    {
                        resultTask = (Task)method.Invoke(instance.Instance, input.Args)!;
                    }
                    else
                    {
                        resultTask = (Task)input.Definition.Delegate!.DynamicInvoke(input.Args)!;
                    }
                    await resultTask.ConfigureAwait(true);
                }
                catch (TargetInvocationException e)
                {
                    ExceptionDispatchInfo.Capture(e.InnerException!).Throw();
                    // Unreachable
                    return null;
                }
                var resultTaskType = resultTask.GetType();
                // We have to use reflection to extract value if it's a Task<>
                if (resultTaskType.IsGenericType)
                {
                    return resultTaskType.GetProperty("Result")!.GetValue(resultTask);
                }
                return null;
            }
        }

        /// <summary>
        /// Workflow outbound implementation.
        /// </summary>
        internal class OutboundImpl : WorkflowOutboundInterceptor
        {
            private readonly WorkflowInstance instance;

            /// <summary>
            /// Initializes a new instance of the <see cref="OutboundImpl"/> class.
            /// </summary>
            /// <param name="instance">Workflow instance.</param>
            public OutboundImpl(WorkflowInstance instance) => this.instance = instance;

            /// <inheritdoc />
            public override Task CancelExternalWorkflowAsync(CancelExternalWorkflowInput input)
            {
                // Add command
                var cmd = new RequestCancelExternalWorkflowExecution()
                {
                    Seq = ++instance.externalCancelsCounter,
                    WorkflowExecution = new()
                    {
                        Namespace = instance.Info.Namespace,
                        WorkflowId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                    },
                };
                var pending = new PendingExternalCancel(
                    SerializationContext: new(Namespace: instance.Info.Namespace, WorkflowId: input.Id),
                    CompletionSource: new());
                instance.externalCancelsPending[cmd.Seq] = pending;
                instance.AddCommand(new() { RequestCancelExternalWorkflowExecution = cmd });

                // Handle
                return instance.QueueNewTaskAsync(async () =>
                {
                    var res = await pending.CompletionSource.Task.ConfigureAwait(true);
                    instance.externalCancelsPending.Remove(cmd.Seq);
                    // Throw if failed
                    if (res.Failure != null)
                    {
                        // Payload and failure converter with context
                        var payloadConverter = instance.payloadConverterNoContext;
                        if (payloadConverter is IWithSerializationContext<IPayloadConverter> withContext)
                        {
                            payloadConverter = withContext.WithSerializationContext(pending.SerializationContext);
                        }
                        var failureConverter = instance.failureConverterNoContext;
                        if (failureConverter is IWithSerializationContext<IFailureConverter> withContext2)
                        {
                            failureConverter = withContext2.WithSerializationContext(pending.SerializationContext);
                        }
                        throw failureConverter.ToException(res.Failure, payloadConverter);
                    }
                });
            }

            /// <inheritdoc />
            public override ContinueAsNewException CreateContinueAsNewException(
                CreateContinueAsNewExceptionInput input) => new(input);

            /// <inheritdoc />
            public override Task DelayAsync(DelayAsyncInput input)
            {
                var token = input.CancellationToken ?? instance.CancellationToken;
                // Task.Delay is started when created not when awaited, so we do the same. Also,
                // they don't create the timer if already cancelled.
                if (token.IsCancellationRequested)
                {
                    return Task.FromCanceled(token);
                }

                // After conferring, it was decided that < -1 is an error and 0 is 1ms and -1 can
                // remain infinite (timer command never created)
                var delay = input.Delay;
                if (delay < TimeSpan.Zero && delay != Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentException("Delay duration cannot be less than 0");
                }
                else if (delay == TimeSpan.Zero)
                {
                    delay = TimeSpan.FromMilliseconds(1);
                }
                var source = new TaskCompletionSource<object?>();
                // Only create the command if not infinite. We use seq 0 to represent uncreated.
                uint seq = 0;
                if (delay != Timeout.InfiniteTimeSpan)
                {
                    seq = ++instance.timerCounter;
                    instance.timersPending[seq] = source;
                    instance.AddCommand(new()
                    {
                        StartTimer = new()
                        {
                            Seq = seq,
                            StartToFireTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(delay),
                        },
                        UserMetadata = new()
                        {
                            Summary = input.Summary == null ?
                                null : instance.payloadConverterWorkflowContext.ToPayload(input.Summary),
                        },
                    });
                }
                return instance.QueueNewTaskAsync(async () =>
                {
                    using (token.Register(() =>
                    {
                        // Try cancel, then send cancel if was able to remove
                        if (source.TrySetCanceled(token) && instance.timersPending.Remove(seq))
                        {
                            instance.AddCommand(new() { CancelTimer = new() { Seq = seq } });
                        }
                    }))
                    {
                        await source.Task.ConfigureAwait(true);
                    }
                });
            }

            /// <inheritdoc />
            public override Task<TResult> ScheduleActivityAsync<TResult>(
                ScheduleActivityInput input)
            {
                if (input.Options.StartToCloseTimeout == null &&
                    input.Options.ScheduleToCloseTimeout == null)
                {
                    throw new ArgumentException("Activity options must have StartToCloseTimeout or ScheduleToCloseTimeout");
                }

                // Get payload converter with context
                var serializationContext = new ISerializationContext.Activity(
                    Namespace: instance.Info.Namespace,
                    WorkflowId: instance.Info.WorkflowId,
                    WorkflowType: instance.Info.WorkflowType,
                    ActivityType: input.Activity,
                    ActivityTaskQueue: input.Options.TaskQueue ?? instance.Info.TaskQueue,
                    IsLocal: false);
                var payloadConverter = instance.payloadConverterNoContext;
                if (payloadConverter is IWithSerializationContext<IPayloadConverter> withContext)
                {
                    payloadConverter = withContext.WithSerializationContext(serializationContext);
                }

                return ExecuteActivityInternalAsync<TResult>(
                    payloadConverter: payloadConverter,
                    serializationContext,
                    doBackoff =>
                    {
                        var seq = ++instance.activityCounter;
                        var cmd = new ScheduleActivity()
                        {
                            Seq = seq,
                            ActivityId = input.Options.ActivityId ?? seq.ToString(),
                            ActivityType = input.Activity,
                            TaskQueue = serializationContext.ActivityTaskQueue,
                            Arguments = { payloadConverter.ToPayloads(input.Args) },
                            RetryPolicy = input.Options.RetryPolicy?.ToProto(),
                            CancellationType = (Bridge.Api.WorkflowCommands.ActivityCancellationType)input.Options.CancellationType,
                            DoNotEagerlyExecute = instance.disableEagerActivityExecution || input.Options.DisableEagerActivityExecution,
                        };
                        if (input.Headers is IDictionary<string, Payload> headers)
                        {
                            cmd.Headers.Add(headers);
                        }
                        if (input.Options.ScheduleToCloseTimeout is TimeSpan schedToClose)
                        {
                            cmd.ScheduleToCloseTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(schedToClose);
                        }
                        if (input.Options.ScheduleToStartTimeout is TimeSpan schedToStart)
                        {
                            cmd.ScheduleToStartTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(schedToStart);
                        }
                        if (input.Options.StartToCloseTimeout is TimeSpan startToClose)
                        {
                            cmd.StartToCloseTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(startToClose);
                        }
                        if (input.Options.HeartbeatTimeout is TimeSpan heartbeat)
                        {
                            cmd.HeartbeatTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(heartbeat);
                        }
#pragma warning disable CS0618
                        if (input.Options.VersioningIntent is { } vi)
                        {
                            cmd.VersioningIntent = (Bridge.Api.Common.VersioningIntent)(int)vi;
                        }
#pragma warning restore CS0618
                        var workflowCommand = new WorkflowCommand() { ScheduleActivity = cmd };
                        if (input.Options.Summary is { } summary)
                        {
                            workflowCommand.UserMetadata = new()
                            {
                                Summary = payloadConverter.ToPayload(summary),
                            };
                        }
                        if (input.Options.Priority is { } priority)
                        {
                            cmd.Priority = priority.ToProto();
                        }
                        instance.AddCommand(workflowCommand);
                        return seq;
                    },
                    input.Options.CancellationToken ?? instance.CancellationToken);
            }

            /// <inheritdoc />
            public override Task<TResult> ScheduleLocalActivityAsync<TResult>(
                ScheduleLocalActivityInput input)
            {
                if (input.Options.StartToCloseTimeout == null &&
                    input.Options.ScheduleToCloseTimeout == null)
                {
                    throw new ArgumentException("Activity options must have StartToCloseTimeout or ScheduleToCloseTimeout");
                }

                instance.AssertValidLocalActivity(input.Activity);

                // Get payload converter with context
                var serializationContext = new ISerializationContext.Activity(
                    Namespace: instance.Info.Namespace,
                    WorkflowId: instance.Info.WorkflowId,
                    WorkflowType: instance.Info.WorkflowType,
                    ActivityType: input.Activity,
                    ActivityTaskQueue: instance.Info.TaskQueue,
                    IsLocal: true);
                var payloadConverter = instance.payloadConverterNoContext;
                if (payloadConverter is IWithSerializationContext<IPayloadConverter> withContext)
                {
                    payloadConverter = withContext.WithSerializationContext(serializationContext);
                }

                return ExecuteActivityInternalAsync<TResult>(
                    payloadConverter: payloadConverter,
                    serializationContext: serializationContext,
                    doBackoff =>
                    {
                        var seq = ++instance.activityCounter;
                        var cmd = new ScheduleLocalActivity()
                        {
                            Seq = seq,
                            ActivityId = input.Options.ActivityId ?? seq.ToString(),
                            ActivityType = input.Activity,
                            Arguments = { payloadConverter.ToPayloads(input.Args) },
                            RetryPolicy = input.Options.RetryPolicy?.ToProto(),
                            CancellationType = (Bridge.Api.WorkflowCommands.ActivityCancellationType)input.Options.CancellationType,
                        };
                        if (input.Headers is IDictionary<string, Payload> headers)
                        {
                            cmd.Headers.Add(headers);
                        }
                        if (input.Options.ScheduleToCloseTimeout is TimeSpan schedToClose)
                        {
                            cmd.ScheduleToCloseTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(schedToClose);
                        }
                        if (input.Options.ScheduleToStartTimeout is TimeSpan schedToStart)
                        {
                            cmd.ScheduleToStartTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(schedToStart);
                        }
                        if (input.Options.StartToCloseTimeout is TimeSpan startToClose)
                        {
                            cmd.StartToCloseTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(startToClose);
                        }
                        if (input.Options.LocalRetryThreshold is TimeSpan localRetry)
                        {
                            cmd.LocalRetryThreshold = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(localRetry);
                        }
                        if (doBackoff != null)
                        {
                            cmd.Attempt = doBackoff.Attempt;
                            cmd.OriginalScheduleTime = doBackoff.OriginalScheduleTime;
                        }
                        var workflowCommand = new WorkflowCommand { ScheduleLocalActivity = cmd };
                        if (input.Options.Summary is { } summary)
                        {
                            workflowCommand.UserMetadata = new()
                            {
                                Summary = payloadConverter.ToPayload(summary),
                            };
                        }
                        instance.AddCommand(workflowCommand);
                        return seq;
                    },
                    input.Options.CancellationToken ?? instance.CancellationToken);
            }

            /// <inheritdoc />
            public override Task SignalChildWorkflowAsync(SignalChildWorkflowInput input)
            {
                // Payload converter with context
                var serializationContext = new ISerializationContext.Workflow(
                    Namespace: instance.Info.Namespace,
                    WorkflowId: input.Id);
                var payloadConverter = instance.payloadConverterNoContext;
                if (payloadConverter is IWithSerializationContext<IPayloadConverter> withContext)
                {
                    payloadConverter = withContext.WithSerializationContext(serializationContext);
                }
                var cmd = new SignalExternalWorkflowExecution()
                {
                    Seq = ++instance.externalSignalsCounter,
                    ChildWorkflowId = input.Id,
                    SignalName = input.Signal,
                    Args = { payloadConverter.ToPayloads(input.Args) },
                };
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
                return SignalExternalWorkflowInternalAsync(
                    serializationContext,
                    payloadConverter,
                    cmd,
                    input.Options?.CancellationToken);
            }

            /// <inheritdoc />
            public override Task SignalExternalWorkflowAsync(SignalExternalWorkflowInput input)
            {
                // Payload converter with context
                var serializationContext = new ISerializationContext.Workflow(
                    Namespace: instance.Info.Namespace,
                    WorkflowId: input.Id);
                var payloadConverter = instance.payloadConverterNoContext;
                if (payloadConverter is IWithSerializationContext<IPayloadConverter> withContext)
                {
                    payloadConverter = withContext.WithSerializationContext(serializationContext);
                }
                var cmd = new SignalExternalWorkflowExecution()
                {
                    Seq = ++instance.externalSignalsCounter,
                    WorkflowExecution = new()
                    {
                        Namespace = instance.Info.Namespace,
                        WorkflowId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                    },
                    SignalName = input.Signal,
                    Args = { payloadConverter.ToPayloads(input.Args) },
                };
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
                return SignalExternalWorkflowInternalAsync(
                    serializationContext,
                    payloadConverter,
                    cmd,
                    input.Options?.CancellationToken);
            }

            /// <inheritdoc />
            public override Task<ChildWorkflowHandle<TWorkflow, TResult>> StartChildWorkflowAsync<TWorkflow, TResult>(
                StartChildWorkflowInput input)
            {
                var token = input.Options.CancellationToken ?? instance.CancellationToken;
                // We do not even want to schedule if the cancellation token is already cancelled.
                // We choose to use cancelled failure instead of wrapping in child failure which is
                // similar to what Java and TypeScript do, with the accepted tradeoff that it makes
                // catch clauses more difficult (hence the presence of
                // TemporalException.IsCanceledException helper).
                if (token.IsCancellationRequested)
                {
                    return Task.FromException<ChildWorkflowHandle<TWorkflow, TResult>>(
                        new CanceledFailureException("Child cancelled before scheduled"));
                }

                // Get payload converter with context
                var serializationContext = new ISerializationContext.Workflow(
                    Namespace: instance.Info.Namespace,
                    WorkflowId: input.Options.Id ?? Workflow.NewGuid().ToString());
                var payloadConverter = instance.payloadConverterNoContext;
                if (payloadConverter is IWithSerializationContext<IPayloadConverter> withContext)
                {
                    payloadConverter = withContext.WithSerializationContext(serializationContext);
                }

                // Add the start command
                var seq = ++instance.childWorkflowCounter;
                var cmd = new StartChildWorkflowExecution()
                {
                    Seq = seq,
                    Namespace = instance.Info.Namespace,
                    WorkflowId = serializationContext.WorkflowId,
                    WorkflowType = input.Workflow,
                    TaskQueue = input.Options.TaskQueue ?? instance.Info.TaskQueue,
                    Input = { payloadConverter.ToPayloads(input.Args) },
                    ParentClosePolicy = (Bridge.Api.ChildWorkflow.ParentClosePolicy)input.Options.ParentClosePolicy,
                    WorkflowIdReusePolicy = input.Options.IdReusePolicy,
                    RetryPolicy = input.Options.RetryPolicy?.ToProto(),
                    CronSchedule = input.Options.CronSchedule ?? string.Empty,
                    CancellationType = (Bridge.Api.ChildWorkflow.ChildWorkflowCancellationType)input.Options.CancellationType,
                };
                if (input.Options.ExecutionTimeout is TimeSpan execTimeout)
                {
                    cmd.WorkflowExecutionTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(execTimeout);
                }
                if (input.Options.RunTimeout is TimeSpan runTimeout)
                {
                    cmd.WorkflowRunTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(runTimeout);
                }
                if (input.Options.TaskTimeout is TimeSpan taskTimeout)
                {
                    cmd.WorkflowTaskTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(taskTimeout);
                }
                if (input.Options?.Memo is IReadOnlyDictionary<string, object> memo)
                {
                    cmd.Memo.Add(memo.ToDictionary(
                        kvp => kvp.Key, kvp => payloadConverter.ToPayload(kvp.Value)));
                }
                if (input.Options?.TypedSearchAttributes is SearchAttributeCollection attrs)
                {
                    cmd.SearchAttributes.Add(attrs.ToProto().IndexedFields);
                }
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
#pragma warning disable CS0618
                if (input.Options?.VersioningIntent is { } vi)
                {
                    cmd.VersioningIntent = (Bridge.Api.Common.VersioningIntent)(int)vi;
                }
#pragma warning restore CS0618
                if (input.Options?.Priority is { } priority)
                {
                    cmd.Priority = priority.ToProto();
                }
                var workflowCommand = new WorkflowCommand() { StartChildWorkflowExecution = cmd };
                if (input.Options?.StaticSummary != null || input.Options?.StaticDetails != null)
                {
                    workflowCommand.UserMetadata = new()
                    {
                        Summary = input.Options.StaticSummary is { } summary ?
                            payloadConverter.ToPayload(summary) : null,
                        Details = input.Options.StaticDetails is { } details ?
                            payloadConverter.ToPayload(details) : null,
                    };
                }
                instance.AddCommand(workflowCommand);

                // Add start as pending and wait inside of task
                var handleSource = new TaskCompletionSource<ChildWorkflowHandle<TWorkflow, TResult>>();
                var pending = new PendingChildInfo(
                    SerializationContext: serializationContext,
                    StartCompletionSource: new(),
                    ResultCompletionSource: new());
                instance.childWorkflowsPending[seq] = pending;
                _ = instance.QueueNewTaskAsync(async () =>
                {
                    try
                    {
                        using (token.Register(() =>
                        {
                            // Send cancel if it's pending
                            if (instance.childWorkflowsPending.ContainsKey(seq))
                            {
                                instance.AddCommand(new()
                                {
                                    CancelChildWorkflowExecution = new() { ChildWorkflowSeq = seq },
                                });
                            }
                        }))
                        {
                            // Wait for start
                            var startRes = await pending.StartCompletionSource.Task.ConfigureAwait(true);
                            // Handle the start result
                            ChildWorkflowHandleImpl<TWorkflow, TResult> handle;
                            switch (startRes.StatusCase)
                            {
                                case ResolveChildWorkflowExecutionStart.StatusOneofCase.Succeeded:
                                    // Create handle
                                    handle = new(instance, payloadConverter, cmd.WorkflowId, startRes.Succeeded.RunId);
                                    break;
                                case ResolveChildWorkflowExecutionStart.StatusOneofCase.Failed:
                                    switch (startRes.Failed.Cause)
                                    {
                                        case StartChildWorkflowExecutionFailedCause.WorkflowAlreadyExists:
                                            handleSource.SetException(
                                                new WorkflowAlreadyStartedException(
                                                    "Child workflow already started",
                                                    workflowId: startRes.Failed.WorkflowId,
                                                    workflowType: startRes.Failed.WorkflowType,
                                                    // Pending https://github.com/temporalio/temporal/issues/6961
                                                    runId: "<unknown>"));
                                            return;
                                        default:
                                            handleSource.SetException(new InvalidOperationException(
                                                $"Unknown child start failed cause: {startRes.Failed.Cause}"));
                                            return;
                                    }
                                case ResolveChildWorkflowExecutionStart.StatusOneofCase.Cancelled:
                                    // Failure converter with context
                                    var failureConverter = instance.failureConverterNoContext;
                                    if (failureConverter is IWithSerializationContext<IFailureConverter> withContext)
                                    {
                                        failureConverter = withContext.WithSerializationContext(serializationContext);
                                    }
                                    handleSource.SetException(
                                        failureConverter.ToException(startRes.Cancelled.Failure, payloadConverter));
                                    return;
                                default:
                                    throw new InvalidOperationException("Unrecognized child start case");
                            }

                            // Resolve handle source
                            handleSource.SetResult(handle);

                            // Wait for completion
                            var completeRes = await pending.ResultCompletionSource.Task.ConfigureAwait(true);

                            // Handle completion
                            switch (completeRes.StatusCase)
                            {
                                case ChildWorkflowResult.StatusOneofCase.Completed:
                                    handle.CompletionSource.SetResult(completeRes.Completed.Result);
                                    break;
                                case ChildWorkflowResult.StatusOneofCase.Failed:
                                    // Failure converter with context
                                    var failureConverter = instance.failureConverterNoContext;
                                    if (failureConverter is IWithSerializationContext<IFailureConverter> withContext)
                                    {
                                        failureConverter = withContext.WithSerializationContext(serializationContext);
                                    }
                                    handle.CompletionSource.SetException(failureConverter.ToException(
                                        completeRes.Failed.Failure_, payloadConverter));
                                    break;
                                case ChildWorkflowResult.StatusOneofCase.Cancelled:
                                    // Failure converter with context
                                    var failureConverter2 = instance.failureConverterNoContext;
                                    if (failureConverter2 is IWithSerializationContext<IFailureConverter> withContext2)
                                    {
                                        failureConverter2 = withContext2.WithSerializationContext(serializationContext);
                                    }
                                    handle.CompletionSource.SetException(failureConverter2.ToException(
                                        completeRes.Cancelled.Failure, payloadConverter));
                                    break;
                                default:
                                    throw new InvalidOperationException("Unrecognized child complete case");
                            }
                        }
                    }
                    finally
                    {
                        instance.childWorkflowsPending.Remove(seq);
                    }
                });
                return handleSource.Task;
            }

            /// <inheritdoc/>
            public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
                StartNexusOperationInput input)
            {
                var token = input.Options.CancellationToken ?? instance.CancellationToken;
                // We do not even want to schedule if the cancellation token is already cancelled.
                // We choose to use cancelled failure instead of wrapping in Nexus failure which is
                // similar to what Java and TypeScript do, with the accepted tradeoff that it makes
                // catch clauses more difficult (hence the presence of
                // TemporalException.IsCanceledException helper).
                if (token.IsCancellationRequested)
                {
                    return Task.FromException<NexusOperationHandle<TResult>>(
                        new CanceledFailureException("Nexus operation cancelled before scheduled"));
                }

                // TODO(cretz): Support Nexus serialization context
                var payloadConverter = instance.payloadConverterNoContext;

                var seq = ++instance.nexusOperationCounter;
                var cmd = new ScheduleNexusOperation()
                {
                    Seq = seq,
                    Endpoint = input.ClientOptions.Endpoint,
                    Service = input.Service,
                    Operation = input.OperationName,
                    Input = input.Arg == null ? null : payloadConverter.ToPayload(input.Arg),
                    CancellationType = (Bridge.Api.Nexus.NexusOperationCancellationType)input.Options.CancellationType,
                };
                if (input.Options.ScheduleToCloseTimeout is TimeSpan schedToCloseTimeout)
                {
                    cmd.ScheduleToCloseTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(schedToCloseTimeout);
                }
                if (input.Headers is IDictionary<string, string> headers)
                {
                    cmd.NexusHeader.Add(headers);
                }
                var workflowCommand = new WorkflowCommand() { ScheduleNexusOperation = cmd };
                if (input.Options.Summary is { } summary)
                {
                    workflowCommand.UserMetadata = new() { Summary = payloadConverter.ToPayload(summary) };
                }
                instance.AddCommand(workflowCommand);

                var handleSource = new TaskCompletionSource<NexusOperationHandle<TResult>>();
                var pending = new PendingNexusOperationInfo(
                    StartCompletionSource: new(),
                    ResultCompletionSource: new());
                instance.nexusOperationsPending[seq] = pending;

                // Wait for start and result inside of task
                _ = instance.QueueNewTaskAsync(async () =>
                {
                    using (token.Register(() =>
                    {
                        // Send cancel if pending
                        if (instance.nexusOperationsPending.ContainsKey(seq))
                        {
                            instance.AddCommand(new()
                            {
                                RequestCancelNexusOperation = new() { Seq = seq },
                            });
                        }
                    }))
                    {
                        try
                        {
                            // Wait for start
                            var startRes = await pending.StartCompletionSource.Task.ConfigureAwait(true);

                            // If there is a start sync fail, we have to fail the handle task and
                            // there's nothing more we can do here
                            var handle = new NexusOperationHandleImpl<TResult>(
                                payloadConverter,
                                // TODO(cretz): Support Nexus serialization context, ideally not
                                // creating failure converter with context until actually needed
                                instance.failureConverterNoContext,
                                startRes.HasOperationToken ? startRes.OperationToken : null);
                            if (startRes.Failed is { } syncStartFail)
                            {
                                // TODO(cretz): Support Nexus serialization context
                                handleSource.SetException(
                                    instance.failureConverterNoContext.ToException(
                                        syncStartFail, payloadConverter));
                                return;
                            }

                            // Set start result
                            handleSource.SetResult(handle);

                            // Wait for completion and set on handle
                            var completeRes = await pending.ResultCompletionSource.Task.ConfigureAwait(true);
                            handle.CompletionSource.SetResult(completeRes);
                        }
                        catch (Exception e)
                        {
                            instance.Logger.LogWarning(e, "Unexpected issue setting Nexus result");
                            instance.SetCurrentActivationException(e);
                        }
                        finally
                        {
                            instance.nexusOperationsPending.Remove(seq);
                        }
                    }
                });
                return handleSource.Task;
            }

            private Task SignalExternalWorkflowInternalAsync(
                ISerializationContext.Workflow serializationContext,
                IPayloadConverter payloadConverter,
                SignalExternalWorkflowExecution cmd,
                CancellationToken? inputCancelToken)
            {
                var token = inputCancelToken ?? instance.CancellationToken;
                // Like other cases (e.g. child workflow start), we do not even want to schedule if
                // the cancellation token is already cancelled.
                if (token.IsCancellationRequested)
                {
                    return Task.FromException(
                        new CanceledFailureException("Signal cancelled before scheduled"));
                }

                var pending = new PendingExternalSignal(
                    SerializationContext: serializationContext,
                    CompletionSource: new());
                instance.externalSignalsPending[cmd.Seq] = pending;
                instance.AddCommand(new() { SignalExternalWorkflowExecution = cmd });

                // Handle
                return instance.QueueNewTaskAsync(async () =>
                {
                    using (token.Register(() =>
                    {
                        // Send cancel if still pending
                        if (instance.externalSignalsPending.ContainsKey(cmd.Seq))
                        {
                            instance.AddCommand(new()
                            {
                                CancelSignalWorkflow = new() { Seq = cmd.Seq },
                            });
                        }
                    }))
                    {
                        var res = await pending.CompletionSource.Task.ConfigureAwait(true);
                        instance.externalSignalsPending.Remove(cmd.Seq);
                        // Throw if failed
                        if (res.Failure != null)
                        {
                            // Failure converter with context
                            var failureConverter = instance.failureConverterNoContext;
                            if (failureConverter is IWithSerializationContext<IFailureConverter> withContext)
                            {
                                failureConverter = withContext.WithSerializationContext(serializationContext);
                            }
                            throw failureConverter.ToException(res.Failure, payloadConverter);
                        }
                    }
                });
            }

            private Task<TResult> ExecuteActivityInternalAsync<TResult>(
                IPayloadConverter payloadConverter,
                ISerializationContext.Activity serializationContext,
                Func<DoBackoff?, uint> applyScheduleCommand,
                CancellationToken cancellationToken)
            {
                // We do not even want to schedule if the cancellation token is already cancelled.
                // We choose to use cancelled failure instead of wrapping in activity failure which
                // is similar to what Java and TypeScript do, with the accepted tradeoff that it
                // makes catch clauses more difficult (hence the presence of
                // TemporalException.IsCanceledException helper).
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromException<TResult>(
                        new CanceledFailureException("Activity cancelled before scheduled"));
                }

                var seq = applyScheduleCommand(null);
                var pending = new PendingActivityInfo(
                    SerializationContext: serializationContext,
                    CompletionSource: new());
                instance.activitiesPending[seq] = pending;
                return instance.QueueNewTaskAsync(async () =>
                {
                    // In a loop for local-activity backoff
                    while (true)
                    {
                        // Run or wait for cancel
                        ActivityResolution res;
                        using (cancellationToken.Register(() =>
                        {
                            // Send cancel if activity present
                            if (instance.activitiesPending.ContainsKey(seq))
                            {
                                if (serializationContext.IsLocal)
                                {
                                    instance.AddCommand(
                                        new() { RequestCancelLocalActivity = new() { Seq = seq } });
                                }
                                else
                                {
                                    instance.AddCommand(
                                        new() { RequestCancelActivity = new() { Seq = seq } });
                                }
                            }
                        }))
                        {
                            res = await pending.CompletionSource.Task.ConfigureAwait(true);
                        }

                        // Apply result. Only DoBackoff will cause loop to continue.
                        instance.activitiesPending.Remove(seq);
                        switch (res.StatusCase)
                        {
                            case ActivityResolution.StatusOneofCase.Completed:
                                // Use default if they are ignoring result or payload not present
                                if (typeof(TResult) == typeof(ValueTuple) || res.Completed.Result == null)
                                {
                                    return default!;
                                }
                                // Otherwise we expect a single payload
                                if (res.Completed.Result == null)
                                {
                                    throw new InvalidOperationException("No activity result present");
                                }
                                return payloadConverter.ToValue<TResult>(res.Completed.Result);
                            case ActivityResolution.StatusOneofCase.Failed:
                                // Failure converter with context
                                var failureConverter = instance.failureConverterNoContext;
                                if (failureConverter is IWithSerializationContext<IFailureConverter> withContext)
                                {
                                    failureConverter = withContext.WithSerializationContext(serializationContext);
                                }
                                throw failureConverter.ToException(res.Failed.Failure_, payloadConverter);
                            case ActivityResolution.StatusOneofCase.Cancelled:
                                // Failure converter with context
                                var failureConverter2 = instance.failureConverterNoContext;
                                if (failureConverter2 is IWithSerializationContext<IFailureConverter> withContext2)
                                {
                                    failureConverter2 = withContext2.WithSerializationContext(serializationContext);
                                }
                                throw failureConverter2.ToException(res.Cancelled.Failure, payloadConverter);
                            case ActivityResolution.StatusOneofCase.Backoff:
                                // We have to sleep the backoff amount. Note, this can be cancelled
                                // like any other timer.
                                await instance.DelayWithOptionsAsync(new(
                                    delay: res.Backoff.BackoffDuration.ToTimeSpan(),
                                    summary: "LocalActivityBackoff",
                                    cancellationToken: cancellationToken)).ConfigureAwait(true);
                                // Re-schedule with backoff info
                                seq = applyScheduleCommand(res.Backoff);
                                pending = pending with { CompletionSource = new TaskCompletionSource<ActivityResolution>() };
                                instance.activitiesPending[seq] = pending;
                                break;
                            default:
                                throw new InvalidOperationException("Activity does not have result");
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Child workflow handle implementation.
        /// </summary>
        /// <typeparam name="TWorkflow">Child workflow type.</typeparam>
        /// <typeparam name="TResult">Child workflow result.</typeparam>
        internal class ChildWorkflowHandleImpl<TWorkflow, TResult> : ChildWorkflowHandle<TWorkflow, TResult>
        {
            private readonly WorkflowInstance instance;
            private readonly IPayloadConverter payloadConverter;
            private readonly string id;
            private readonly string firstExecutionRunId;

            /// <summary>
            /// Initializes a new instance of the <see cref="ChildWorkflowHandleImpl{TWorkflow, TResult}"/> class.
            /// </summary>
            /// <param name="instance">Workflow instance.</param>
            /// <param name="payloadConverter">Payload converter.</param>
            /// <param name="id">Workflow ID.</param>
            /// <param name="firstExecutionRunId">Workflow run ID.</param>
            public ChildWorkflowHandleImpl(
                WorkflowInstance instance,
                IPayloadConverter payloadConverter,
                string id,
                string firstExecutionRunId)
            {
                this.instance = instance;
                this.payloadConverter = payloadConverter;
                this.id = id;
                this.firstExecutionRunId = firstExecutionRunId;
            }

            /// <inheritdoc />
            public override string Id => id;

            /// <inheritdoc />
            public override string FirstExecutionRunId => firstExecutionRunId;

            /// <summary>
            /// Gets the source for the resulting payload of the child.
            /// </summary>
            internal TaskCompletionSource<Payload?> CompletionSource { get; } = new();

            /// <inheritdoc />
            public override async Task<TLocalResult> GetResultAsync<TLocalResult>()
            {
                var payload = await CompletionSource.Task.ConfigureAwait(true);
                // Use default if they are ignoring result or payload not present
                if (typeof(TLocalResult) == typeof(ValueTuple) || payload == null)
                {
                    return default!;
                }
                return payloadConverter.ToValue<TLocalResult>(payload);
            }

            /// <inheritdoc />
            public override Task SignalAsync(
                string signal,
                IReadOnlyCollection<object?> args,
                ChildWorkflowSignalOptions? options = null) =>
                instance.outbound.Value.SignalChildWorkflowAsync(new(
                    Id: Id,
                    Signal: signal,
                    Args: args,
                    Options: options,
                    Headers: null));
        }

        /// <summary>
        /// External workflow handle implementation.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        internal class ExternalWorkflowHandleImpl<TWorkflow> : ExternalWorkflowHandle<TWorkflow>
        {
            private readonly WorkflowInstance instance;
            private readonly string id;
            private readonly string? runId;

            /// <summary>
            /// Initializes a new instance of the <see cref="ExternalWorkflowHandleImpl{TWorkflow}"/>
            /// class.
            /// </summary>
            /// <param name="instance">Workflow instance.</param>
            /// <param name="id">Workflow ID.</param>
            /// <param name="runId">Workflow run ID.</param>
            public ExternalWorkflowHandleImpl(WorkflowInstance instance, string id, string? runId)
            {
                this.instance = instance;
                this.id = id;
                this.runId = runId;
            }

            /// <inheritdoc />
            public override string Id => id;

            /// <inheritdoc />
            public override string? RunId => runId;

            /// <inheritdoc />
            public override Task SignalAsync(
                string signal,
                IReadOnlyCollection<object?> args,
                ExternalWorkflowSignalOptions? options = null) =>
                instance.outbound.Value.SignalExternalWorkflowAsync(new(
                    Id: Id,
                    RunId: RunId,
                    Signal: signal,
                    Args: args,
                    Options: options,
                    Headers: null));

            /// <inheritdoc />
            public override Task CancelAsync() =>
                instance.outbound.Value.CancelExternalWorkflowAsync(new(Id: Id, RunId: RunId));
        }

        private class NexusClientImpl : NexusClient
        {
            private readonly WorkflowInstance instance;

            public NexusClientImpl(WorkflowInstance instance, string service, NexusClientOptions options)
            {
                this.instance = instance;
                Service = service;
                Options = options;
            }

            public override string Service { get; }

            public override NexusClientOptions Options { get; }

            public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
                string operationName, object? arg, NexusOperationOptions? options = null) =>
                instance.outbound.Value.StartNexusOperationAsync<TResult>(new(
                    Service: Service,
                    ClientOptions: Options,
                    OperationName: operationName,
                    Arg: arg,
                    Options: options ?? new(),
                    Headers: null));
        }

        private class NexusClientImpl<TService> : NexusClient<TService>
        {
            private readonly WorkflowInstance instance;

            public NexusClientImpl(WorkflowInstance instance, NexusClientOptions options)
            {
                this.instance = instance;
                ServiceDefinition = ServiceDefinition.FromType<TService>();
                Options = options;
            }

            public override ServiceDefinition ServiceDefinition { get; }

            public override NexusClientOptions Options { get; }

            public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
                string operationName, object? arg, NexusOperationOptions? options = null) =>
                instance.outbound.Value.StartNexusOperationAsync<TResult>(new(
                    Service: Service,
                    ClientOptions: Options,
                    OperationName: operationName,
                    Arg: arg,
                    Options: options ?? new(),
                    Headers: null));
        }

        private class NexusOperationHandleImpl<TResult> : NexusOperationHandle<TResult>
        {
            private readonly IPayloadConverter payloadConverter;
            private readonly IFailureConverter failureConverter;

            public NexusOperationHandleImpl(
                IPayloadConverter payloadConverter,
                IFailureConverter failureConverter,
                string? operationToken)
            {
                this.payloadConverter = payloadConverter;
                this.failureConverter = failureConverter;
                OperationToken = operationToken;
            }

            public override string? OperationToken { get; }

            internal TaskCompletionSource<NexusOperationResult> CompletionSource { get; } = new();

            public override async Task<TLocalResult> GetResultAsync<TLocalResult>()
            {
                var res = await CompletionSource.Task.ConfigureAwait(true);
                // If completed, return that
                if (res.Completed is { } completed)
                {
                    // Use default if they are ignoring result or payload not present
                    if (typeof(TLocalResult) == typeof(ValueTuple))
                    {
                        return default!;
                    }
                    return payloadConverter.ToValue<TLocalResult>(completed);
                }
                // Throw failure
                throw CreateResultFailure(res);
            }

            private Exception CreateResultFailure(NexusOperationResult res)
            {
                // Return if completed or extract failure if failed
                Api.Failure.V1.Failure failure;
                switch (res.StatusCase)
                {
                    case NexusOperationResult.StatusOneofCase.Failed:
                        failure = res.Failed;
                        break;
                    case NexusOperationResult.StatusOneofCase.Cancelled:
                        failure = res.Cancelled;
                        break;
                    case NexusOperationResult.StatusOneofCase.TimedOut:
                        failure = res.TimedOut;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unrecognized operation result status {res.StatusCase}");
                }
                // Convert failure
                return failureConverter.ToException(failure, payloadConverter);
            }
        }

        private record PendingActivityInfo(
            ISerializationContext.Activity SerializationContext,
            TaskCompletionSource<ActivityResolution> CompletionSource);

        private record PendingChildInfo(
            ISerializationContext.Workflow SerializationContext,
            TaskCompletionSource<ResolveChildWorkflowExecutionStart> StartCompletionSource,
            TaskCompletionSource<ChildWorkflowResult> ResultCompletionSource);

        private record PendingExternalSignal(
            ISerializationContext.Workflow SerializationContext,
            TaskCompletionSource<ResolveSignalExternalWorkflow> CompletionSource);

        private record PendingExternalCancel(
            ISerializationContext.Workflow SerializationContext,
            TaskCompletionSource<ResolveRequestCancelExternalWorkflow> CompletionSource);

        private record PendingNexusOperationInfo(
            TaskCompletionSource<ResolveNexusOperationStart> StartCompletionSource,
            TaskCompletionSource<NexusOperationResult> ResultCompletionSource);

        private class Handlers : LinkedList<Handlers.Handler>
        {
#pragma warning disable SA1118 // We're ok w/ string literals spanning lines
            private static readonly Action<ILogger, string, WarnableSignals, Exception?> SignalWarning =
                LoggerMessage.Define<string, WarnableSignals>(
                    LogLevel.Warning,
                    0,
                    "[TMPRL1102] Workflow {Id} finished while signal handlers are still running. This may " +
                    "have interrupted work that the signal handler was doing. You can wait for " +
                    "all update and signal handlers to complete by using `await " +
                    "Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished)`. " +
                    "Alternatively, if both you and the clients sending the signal are okay with " +
                    "interrupting running handlers when the workflow finishes, " +
                    "then you can disable this warning via the signal " +
                    "handler attribute: " +
                    "`[WorkflowSignal(UnfinishedPolicy=HandlerUnfinishedPolicy.Abandon)]`. The " +
                    "following signals were unfinished (and warnings were not disabled for their " +
                    "handler): {Handlers}");

            private static readonly Action<ILogger, string, WarnableUpdates, Exception?> UpdateWarning =
                LoggerMessage.Define<string, WarnableUpdates>(
                    LogLevel.Warning,
                    0,
                    "[TMPRL1102] Workflow {Id} finished while update handlers are still running. This may " +
                    "have interrupted work that the update handler was doing, and the client " +
                    "that sent the update will receive a 'workflow update was aborted by closing workflow'" +
                    "RpcException instead of the update result. You can wait for all update and " +
                    "signal handlers to complete by using `await " +
                    "Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished)`. " +
                    "Alternatively, if both you and the clients sending the update are okay with " +
                    "interrupting running handlers when the workflow finishes, and causing " +
                    "clients to receive errors, then you can disable this warning via the update " +
                    "handler attribute: " +
                    "`[WorkflowUpdate(UnfinishedPolicy=HandlerUnfinishedPolicy.Abandon)]`. The " +
                    "following updates were unfinished (and warnings were not disabled for their " +
                    "handler): {Handlers}");
#pragma warning restore SA1118

            public void WarnIfAnyLeftOver(string id, ILogger logger)
            {
                var signals = this.
                    Where(h => h.UpdateId == null && h.UnfinishedPolicy == HandlerUnfinishedPolicy.WarnAndAbandon).
                    GroupBy(h => h.Name).
                    Select(h => (h.Key, h.Count())).
                    ToArray();
                if (signals.Length > 0)
                {
                    SignalWarning(logger, id, new WarnableSignals { NamesAndCounts = signals }, null);
                }
                var updates = this.
                    Where(h => h.UpdateId != null && h.UnfinishedPolicy == HandlerUnfinishedPolicy.WarnAndAbandon).
                    Select(h => (h.Name, h.UpdateId!)).
                    ToArray();
                if (updates.Length > 0)
                {
                    UpdateWarning(logger, id, new WarnableUpdates { NamesAndIds = updates }, null);
                }
            }

            public readonly struct WarnableSignals
            {
                public (string, int)[] NamesAndCounts { get; init; }

                public override string ToString() => JsonSerializer.Serialize(
                    NamesAndCounts.Select(v => new { name = v.Item1, count = v.Item2 }).ToArray());
            }

            public readonly struct WarnableUpdates
            {
                public (string, string)[] NamesAndIds { get; init; }

                public override string ToString() => JsonSerializer.Serialize(
                    NamesAndIds.Select(v => new { name = v.Item1, id = v.Item2 }).ToArray());
            }

            public record Handler(
                string Name,
                string? UpdateId,
                HandlerUnfinishedPolicy UnfinishedPolicy);
        }
    }
}
