#pragma warning disable CA1001 // We know that we have a cancellation token source we instead clean on destruct
#pragma warning disable CA1031 // We do want to catch _all_ exceptions in this file sometimes

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Api.Common.V1;
using Temporalio.Bridge.Api.ActivityResult;
using Temporalio.Bridge.Api.ChildWorkflow;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCommands;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Instance of a workflow execution.
    /// </summary>
    internal class WorkflowInstance : TaskScheduler, IWorkflowInstance, IWorkflowContext
    {
        private static readonly string[] Newlines = new[] { "\r", "\n", "\r\n" };
        private static readonly AsyncLocal<WorkflowUpdateInfo> CurrentUpdateInfoLocal = new();

        private readonly TaskFactory taskFactory;
        private readonly IFailureConverter failureConverter;
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
        private readonly Dictionary<uint, TaskCompletionSource<ActivityResolution>> activitiesPending = new();
        private readonly Dictionary<uint, TaskCompletionSource<ResolveChildWorkflowExecutionStart>> childWorkflowsPendingStart = new();
        private readonly Dictionary<uint, TaskCompletionSource<ChildWorkflowResult>> childWorkflowsPendingCompletion = new();
        private readonly Dictionary<uint, TaskCompletionSource<ResolveSignalExternalWorkflow>> externalSignalsPending = new();
        private readonly Dictionary<uint, TaskCompletionSource<ResolveRequestCancelExternalWorkflow>> externalCancelsPending = new();
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
        private WorkflowActivationCompletion? completion;
        // Will be set to null after last use (i.e. when workflow actually started)
        private Lazy<object?[]>? startArgs;
        private object? instance;
        private Exception? currentActivationException;
        private uint timerCounter;
        private uint activityCounter;
        private uint childWorkflowCounter;
        private uint externalSignalsCounter;
        private uint externalCancelsCounter;
        private WorkflowQueryDefinition? dynamicQuery;
        private WorkflowSignalDefinition? dynamicSignal;
        private WorkflowUpdateDefinition? dynamicUpdate;

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
            PayloadConverter = details.PayloadConverter;
            failureConverter = details.FailureConverter;
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
            var initialMemo = details.Start.Memo;
            memo = new(
                () => initialMemo == null ? new Dictionary<string, IRawValue>(0) :
                    initialMemo.Fields.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (IRawValue)new RawValue(kvp.Value)),
                false);
            var initialSearchAttributes = details.Start.SearchAttributes;
            typedSearchAttributes = new(
                () => initialSearchAttributes == null ? new(new()) :
                    SearchAttributeCollection.FromProto(initialSearchAttributes),
                false);
            var act = details.InitialActivation;
            CurrentBuildId = act.BuildIdForCurrentTask;
            var start = details.Start;
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
            initialSearchAttributes = details.Start.SearchAttributes;
            WorkflowInfo.ParentInfo? parent = null;
            if (start.ParentWorkflowInfo != null)
            {
                parent = new(
                    Namespace: start.ParentWorkflowInfo.Namespace,
                    RunId: start.ParentWorkflowInfo.RunId,
                    WorkflowId: start.ParentWorkflowInfo.WorkflowId);
            }
            var lastFailure = start.ContinuedFailure == null ?
                null : failureConverter.ToException(start.ContinuedFailure, PayloadConverter);
            var lastResult = start.LastCompletionResult?.Payloads_.Select(v => new RawValue(v)).ToArray();
            static string? NonEmptyOrNull(string s) => string.IsNullOrEmpty(s) ? null : s;
            Info = new(
                Attempt: start.Attempt,
                ContinuedRunId: NonEmptyOrNull(start.ContinuedFromExecutionRunId),
                CronSchedule: NonEmptyOrNull(start.CronSchedule),
                ExecutionTimeout: start.WorkflowExecutionTimeout?.ToTimeSpan(),
                Headers: start.Headers,
                LastFailure: lastFailure,
                LastResult: lastResult,
                Namespace: details.Namespace,
                Parent: parent,
                RetryPolicy: start.RetryPolicy == null ? null : Common.RetryPolicy.FromProto(start.RetryPolicy),
                RunId: act.RunId,
                RunTimeout: start.WorkflowRunTimeout?.ToTimeSpan(),
                StartTime: act.Timestamp.ToDateTime(),
                TaskQueue: details.TaskQueue,
                TaskTimeout: start.WorkflowTaskTimeout.ToTimeSpan(),
                WorkflowId: start.WorkflowId,
                WorkflowType: start.WorkflowType);
            workflowStackTrace = details.WorkflowStackTrace;
            pendingTaskStackTraces = workflowStackTrace == WorkflowStackTrace.None ? null : new();
            logger = details.LoggerFactory.CreateLogger($"Temporalio.Workflow:{start.WorkflowType}");
            replaySafeLogger = new(logger);
            onTaskStarting = details.OnTaskStarting;
            onTaskCompleted = details.OnTaskCompleted;
            Random = new(details.Start.RandomnessSeed);
            TracingEventsEnabled = !details.DisableTracingEvents;
            workerLevelFailureExceptionTypes = details.WorkerLevelFailureExceptionTypes;
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
        public bool TracingEventsEnabled { get; private init; }

        /// <inheritdoc />
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        /// <inheritdoc />
        public bool ContinueAsNewSuggested { get; private set; }

        /// <inheritdoc />
        public string CurrentBuildId { get; private set; }

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
        public bool IsReplaying { get; private set; }

        /// <inheritdoc />
        public ILogger Logger => replaySafeLogger;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IRawValue> Memo => memo.Value;

        /// <inheritdoc />
        public MetricMeter MetricMeter => metricMeter.Value;

        /// <inheritdoc />
        public IPayloadConverter PayloadConverter { get; private init; }

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
        /// Gets the instance, lazily creating if needed. This should never be called outside this
        /// scheduler.
        /// </summary>
        private object Instance
        {
            get
            {
                // We create this lazily because we want the constructor in a workflow context
                instance ??= Definition.CreateWorkflowInstance(startArgs!.Value);
                return instance;
            }
        }

        /// <inheritdoc/>
        public ContinueAsNewException CreateContinueAsNewException(
            string workflow, IReadOnlyCollection<object?> args, ContinueAsNewOptions? options) =>
            outbound.Value.CreateContinueAsNewException(new(
                Workflow: workflow,
                Args: args,
                Options: options,
                Headers: null));

        /// <inheritdoc/>
        public Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken) =>
            outbound.Value.DelayAsync(new(Delay: delay, CancellationToken: cancellationToken));

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
                    upsertedMemo.Fields[update.UntypedKey] = PayloadConverter.ToPayload(
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
        public Task<bool> WaitConditionAsync(
            Func<bool> conditionCheck,
            TimeSpan? timeout,
            CancellationToken? cancellationToken)
        {
            var source = new TaskCompletionSource<object?>();
            var node = conditions.AddLast(Tuple.Create(conditionCheck, source));
            var token = cancellationToken ?? CancellationToken;
            return QueueNewTaskAsync(async () =>
            {
                try
                {
                    using (token.Register(() => source.TrySetCanceled(token)))
                    {
                        // If there's no timeout, it'll never return false, so just wait
                        if (timeout == null)
                        {
                            await source.Task.ConfigureAwait(true);
                            return true;
                        }
                        // Try a timeout that we cancel if never hit
                        using (var delayCancelSource = new CancellationTokenSource())
                        {
                            var completedTask = await Task.WhenAny(source.Task, DelayAsync(
                                timeout.GetValueOrDefault(), delayCancelSource.Token)).ConfigureAwait(true);
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
        public WorkflowActivationCompletion Activate(WorkflowActivation act)
        {
            using (logger.BeginScope(Info.LoggerScope))
            {
                completion = new() { RunId = act.RunId, Successful = new() };
                currentActivationException = null;
                ContinueAsNewSuggested = act.ContinueAsNewSuggested;
                CurrentHistoryLength = checked((int)act.HistoryLength);
                CurrentHistorySize = checked((int)act.HistorySizeBytes);
                CurrentBuildId = act.BuildIdForCurrentTask;
                IsReplaying = act.IsReplaying;
                UtcNow = act.Timestamp.ToDateTime();

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
                        // We can trust jobs are deterministically ordered by core
                        foreach (var job in act.Jobs)
                        {
                            Apply(job);
                            // Run scheduler once. Do not check conditions when patching or querying.
                            var checkConditions = job.NotifyHasPatch == null && job.QueryWorkflow == null;
                            RunOnce(checkConditions);
                        }
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
                            Failure_ = failureConverter.ToFailure(e, PayloadConverter),
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

                // Remove any non-query commands after terminal commands
                if (completion.Successful != null)
                {
                    var seenCompletion = false;
                    var i = 0;
                    while (i < completion.Successful.Commands.Count)
                    {
                        var cmd = completion.Successful.Commands[i];
                        if (!seenCompletion)
                        {
                            seenCompletion = cmd.CompleteWorkflowExecution != null ||
                                cmd.ContinueAsNewWorkflowExecution != null ||
                                cmd.FailWorkflowExecution != null;
                        }
                        else if (cmd.RespondToQuery != null)
                        {
                            completion.Successful.Commands.RemoveAt(i);
                            continue;
                        }
                        i++;
                    }
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
                        foreach (var source in conditions.Where(t => t.Item1()).Select(t => t.Item2))
                        {
                            source.TrySetResult(null);
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
                        Arguments = { PayloadConverter.ToPayloads(e.Input.Args) },
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
                            kvp => kvp.Key, kvp => PayloadConverter.ToPayload(kvp.Value)));
                    }
                    if (e.Input.Options?.TypedSearchAttributes is SearchAttributeCollection attrs)
                    {
                        cmd.SearchAttributes.Add(attrs.ToProto().IndexedFields);
                    }
                    if (e.Input.Headers is IDictionary<string, Payload> headers)
                    {
                        cmd.Headers.Add(headers);
                    }
                    if (e.Input.Options?.VersioningIntent is { } vi)
                    {
                        cmd.VersioningIntent = (Bridge.Api.Common.VersioningIntent)(int)vi;
                    }
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
                    var failure = failureConverter.ToFailure(e, PayloadConverter);
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
                Definition.FailureExceptionTypes?.Any(t => t.IsAssignableFrom(e.GetType())) == true ||
                workerLevelFailureExceptionTypes?.Any(t => t.IsAssignableFrom(e.GetType())) == true;

        private void Apply(WorkflowActivationJob job)
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
                case WorkflowActivationJob.VariantOneofCase.ResolveRequestCancelExternalWorkflow:
                    ApplyResolveRequestCancelExternalWorkflow(job.ResolveRequestCancelExternalWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.ResolveSignalExternalWorkflow:
                    ApplyResolveSignalExternalWorkflow(job.ResolveSignalExternalWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.SignalWorkflow:
                    ApplySignalWorkflow(job.SignalWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.StartWorkflow:
                    ApplyStartWorkflow(job.StartWorkflow);
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
                // Set the current update for the life of this task
                CurrentUpdateInfoLocal.Value = new(Id: update.Id, Name: update.Name);

                // Find update definition or reject
                var updates = mutableUpdates.IsValueCreated ? mutableUpdates.Value : Definition.Updates;
                if (!updates.TryGetValue(update.Name, out var updateDefn))
                {
                    updateDefn = DynamicUpdate;
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
                                Rejected = failureConverter.ToFailure(failure, PayloadConverter),
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
                            Rejected = failureConverter.ToFailure(e, PayloadConverter),
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
                    return task.ContinueWith(
                        _ =>
                        {
                            // If workflow failure exception, it's an update failure. If it's some
                            // other exception, it's a task failure. Otherwise it's a success.
                            var exc = task.Exception?.InnerExceptions?.SingleOrDefault();
                            if (exc != null && IsWorkflowFailureException(exc))
                            {
                                AddCommand(new()
                                {
                                    UpdateResponse = new()
                                    {
                                        ProtocolInstanceId = update.ProtocolInstanceId,
                                        Rejected = failureConverter.ToFailure(exc, PayloadConverter),
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
                                var taskType = task.GetType();
                                var result = taskType.IsGenericType ?
                                    taskType.GetProperty("Result")!.GetValue(task) : ValueTuple.Create();
                                AddCommand(new()
                                {
                                    UpdateResponse = new()
                                    {
                                        ProtocolInstanceId = update.ProtocolInstanceId,
                                        Completed = PayloadConverter.ToPayload(result),
                                    },
                                });
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
                            Rejected = failureConverter.ToFailure(e, PayloadConverter),
                        },
                    });
                    return Task.CompletedTask;
                }
            });
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
                var origCmdCount = completion?.Successful?.Commands?.Count;
                try
                {
                    WorkflowQueryDefinition? queryDefn;
                    // If it's a stack trace query, create definition
                    if (query.QueryType == "__stack_trace")
                    {
                        Func<string> getter = GetStackTrace;
                        queryDefn = WorkflowQueryDefinition.CreateWithoutAttribute(
                            "__stack_trace", getter);
                    }
                    else
                    {
                        // Find definition or fail
                        var queries = mutableQueries.IsValueCreated ? mutableQueries.Value : Definition.Queries;
                        if (!queries.TryGetValue(query.QueryType, out queryDefn))
                        {
                            queryDefn = DynamicQuery;
                            if (queryDefn == null)
                            {
                                var knownQueries = queries.Keys.OrderBy(k => k);
                                throw new InvalidOperationException(
                                    $"Query handler for {query.QueryType} expected but not found, " +
                                    $"known queries: [{string.Join(" ", knownQueries)}]");
                            }
                        }
                    }
                    var resultObj = inbound.Value.HandleQuery(new(
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
                    AddCommand(new()
                    {
                        RespondToQuery = new()
                        {
                            QueryId = query.QueryId,
                            Succeeded = new() { Response = PayloadConverter.ToPayload(resultObj) },
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
                            Failed = failureConverter.ToFailure(e, PayloadConverter),
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
            if (!activitiesPending.TryGetValue(resolve.Seq, out var source))
            {
                throw new InvalidOperationException(
                    $"Failed finding activity for sequence {resolve.Seq}");
            }
            source.TrySetResult(resolve.Result);
        }

        private void ApplyResolveChildWorkflowExecution(ResolveChildWorkflowExecution resolve)
        {
            if (!childWorkflowsPendingCompletion.TryGetValue(resolve.Seq, out var source))
            {
                throw new InvalidOperationException(
                    $"Failed finding child for sequence {resolve.Seq}");
            }
            source.TrySetResult(resolve.Result);
        }

        private void ApplyResolveChildWorkflowExecutionStart(
            ResolveChildWorkflowExecutionStart resolve)
        {
            if (!childWorkflowsPendingStart.TryGetValue(resolve.Seq, out var source))
            {
                throw new InvalidOperationException(
                    $"Failed finding child for sequence {resolve.Seq}");
            }
            source.TrySetResult(resolve);
        }

        private void ApplyResolveRequestCancelExternalWorkflow(
            ResolveRequestCancelExternalWorkflow resolve)
        {
            if (!externalCancelsPending.TryGetValue(resolve.Seq, out var source))
            {
                throw new InvalidOperationException(
                    $"Failed finding external cancel for sequence {resolve.Seq}");
            }
            source.TrySetResult(resolve);
        }

        private void ApplyResolveSignalExternalWorkflow(ResolveSignalExternalWorkflow resolve)
        {
            if (!externalSignalsPending.TryGetValue(resolve.Seq, out var source))
            {
                throw new InvalidOperationException(
                    $"Failed finding external signal for sequence {resolve.Seq}");
            }
            source.TrySetResult(resolve);
        }

        private void ApplySignalWorkflow(SignalWorkflow signal)
        {
            // Find applicable definition or buffer
            var signals = mutableSignals.IsValueCreated ? mutableSignals.Value : Definition.Signals;
            if (!signals.TryGetValue(signal.SignalName, out var signalDefn))
            {
                signalDefn = DynamicSignal;
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

                await inbound.Value.HandleSignalAsync(new(
                    Signal: signal.SignalName,
                    Definition: signalDefn,
                    Args: args,
                    Headers: signal.Headers)).ConfigureAwait(true);
            }));
        }

        private void ApplyStartWorkflow(StartWorkflow start)
        {
            _ = QueueNewTaskAsync(() => RunTopLevelAsync(async () =>
            {
                var input = new ExecuteWorkflowInput(
                    Instance: Instance,
                    RunMethod: Definition.RunMethod,
                    Args: startArgs!.Value);
                // We no longer need start args after this point, so we are unsetting them
                startArgs = null;
                var resultObj = await inbound.Value.ExecuteWorkflowAsync(input).ConfigureAwait(true);
                var result = PayloadConverter.ToPayload(resultObj);
                AddCommand(new() { CompleteWorkflowExecution = new() { Result = result } });
            }));
        }

        private void ApplyUpdateRandomSeed(UpdateRandomSeed update) =>
            Random = new(update.RandomnessSeed);

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
            IReadOnlyCollection<Payload> payloads,
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
                        PayloadConverter.ToValue(payload, paramInfo.ParameterType)));
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
                var source = new TaskCompletionSource<ResolveRequestCancelExternalWorkflow>();
                instance.externalCancelsPending[cmd.Seq] = source;
                instance.AddCommand(new() { RequestCancelExternalWorkflowExecution = cmd });

                // Handle
                return instance.QueueNewTaskAsync(async () =>
                {
                    var res = await source.Task.ConfigureAwait(true);
                    instance.externalCancelsPending.Remove(cmd.Seq);
                    // Throw if failed
                    if (res.Failure != null)
                    {
                        throw instance.failureConverter.ToException(
                            res.Failure, instance.PayloadConverter);
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

                return ExecuteActivityInternalAsync<TResult>(
                    local: false,
                    doBackoff =>
                    {
                        var seq = ++instance.activityCounter;
                        var cmd = new ScheduleActivity()
                        {
                            Seq = seq,
                            ActivityId = input.Options.ActivityId ?? seq.ToString(),
                            ActivityType = input.Activity,
                            TaskQueue = input.Options.TaskQueue ?? instance.Info.TaskQueue,
                            Arguments = { instance.PayloadConverter.ToPayloads(input.Args) },
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
                        if (input.Options.HeartbeatTimeout is TimeSpan heartbeat)
                        {
                            cmd.HeartbeatTimeout = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(heartbeat);
                        }
                        if (input.Options.VersioningIntent is { } vi)
                        {
                            cmd.VersioningIntent = (Bridge.Api.Common.VersioningIntent)(int)vi;
                        }
                        instance.AddCommand(new() { ScheduleActivity = cmd });
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

                return ExecuteActivityInternalAsync<TResult>(
                    local: true,
                    doBackoff =>
                    {
                        var seq = ++instance.activityCounter;
                        var cmd = new ScheduleLocalActivity()
                        {
                            Seq = seq,
                            ActivityId = input.Options.ActivityId ?? seq.ToString(),
                            ActivityType = input.Activity,
                            Arguments = { instance.PayloadConverter.ToPayloads(input.Args) },
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
                        instance.AddCommand(new() { ScheduleLocalActivity = cmd });
                        return seq;
                    },
                    input.Options.CancellationToken ?? instance.CancellationToken);
            }

            /// <inheritdoc />
            public override Task SignalChildWorkflowAsync(SignalChildWorkflowInput input)
            {
                var cmd = new SignalExternalWorkflowExecution()
                {
                    Seq = ++instance.externalSignalsCounter,
                    ChildWorkflowId = input.Id,
                    SignalName = input.Signal,
                    Args = { instance.PayloadConverter.ToPayloads(input.Args) },
                };
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
                return SignalExternalWorkflowInternalAsync(cmd, input.Options?.CancellationToken);
            }

            /// <inheritdoc />
            public override Task SignalExternalWorkflowAsync(SignalExternalWorkflowInput input)
            {
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
                    Args = { instance.PayloadConverter.ToPayloads(input.Args) },
                };
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
                return SignalExternalWorkflowInternalAsync(cmd, input.Options?.CancellationToken);
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

                // Add the start command
                var seq = ++instance.childWorkflowCounter;
                var cmd = new StartChildWorkflowExecution()
                {
                    Seq = seq,
                    Namespace = instance.Info.Namespace,
                    WorkflowId = input.Options.Id ?? Workflow.NewGuid().ToString(),
                    WorkflowType = input.Workflow,
                    TaskQueue = input.Options.TaskQueue ?? instance.Info.TaskQueue,
                    Input = { instance.PayloadConverter.ToPayloads(input.Args) },
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
                        kvp => kvp.Key, kvp => instance.PayloadConverter.ToPayload(kvp.Value)));
                }
                if (input.Options?.TypedSearchAttributes is SearchAttributeCollection attrs)
                {
                    cmd.SearchAttributes.Add(attrs.ToProto().IndexedFields);
                }
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
                if (input.Options?.VersioningIntent is { } vi)
                {
                    cmd.VersioningIntent = (Bridge.Api.Common.VersioningIntent)(int)vi;
                }
                instance.AddCommand(new() { StartChildWorkflowExecution = cmd });

                // Add start as pending and wait inside of task
                var handleSource = new TaskCompletionSource<ChildWorkflowHandle<TWorkflow, TResult>>();
                var startSource = new TaskCompletionSource<ResolveChildWorkflowExecutionStart>();
                instance.childWorkflowsPendingStart[seq] = startSource;

                _ = instance.QueueNewTaskAsync(async () =>
                {
                    using (token.Register(() =>
                    {
                        // Send cancel if in either pending dict
                        if (instance.childWorkflowsPendingStart.ContainsKey(seq) ||
                            instance.childWorkflowsPendingCompletion.ContainsKey(seq))
                        {
                            instance.AddCommand(new()
                            {
                                CancelChildWorkflowExecution = new() { ChildWorkflowSeq = seq },
                            });
                        }
                    }))
                    {
                        // Wait for start
                        var startRes = await startSource.Task.ConfigureAwait(true);
                        // Remove pending
                        instance.childWorkflowsPendingStart.Remove(seq);
                        // Handle the start result
                        ChildWorkflowHandleImpl<TWorkflow, TResult> handle;
                        switch (startRes.StatusCase)
                        {
                            case ResolveChildWorkflowExecutionStart.StatusOneofCase.Succeeded:
                                // Create handle
                                handle = new(instance, cmd.WorkflowId, startRes.Succeeded.RunId);
                                break;
                            case ResolveChildWorkflowExecutionStart.StatusOneofCase.Failed:
                                switch (startRes.Failed.Cause)
                                {
                                    case StartChildWorkflowExecutionFailedCause.WorkflowAlreadyExists:
                                        handleSource.SetException(
                                            new WorkflowAlreadyStartedException(
                                                "Child workflow already started",
                                                startRes.Failed.WorkflowId,
                                                startRes.Failed.WorkflowType));
                                        return;
                                    default:
                                        handleSource.SetException(new InvalidOperationException(
                                            $"Unknown child start failed cause: {startRes.Failed.Cause}"));
                                        return;
                                }
                            case ResolveChildWorkflowExecutionStart.StatusOneofCase.Cancelled:
                                handleSource.SetException(
                                    instance.failureConverter.ToException(
                                        startRes.Cancelled.Failure, instance.PayloadConverter));
                                return;
                            default:
                                throw new InvalidOperationException("Unrecognized child start case");
                        }

                        // Create task for waiting for pending result, then resolve handle source
                        var completionSource = new TaskCompletionSource<ChildWorkflowResult>();
                        instance.childWorkflowsPendingCompletion[seq] = completionSource;
                        handleSource.SetResult(handle);

                        // Wait for completion
                        var completeRes = await completionSource.Task.ConfigureAwait(true);
                        instance.childWorkflowsPendingCompletion.Remove(seq);

                        // Handle completion
                        switch (completeRes.StatusCase)
                        {
                            case ChildWorkflowResult.StatusOneofCase.Completed:
                                // We expect a single payload
                                if (completeRes.Completed.Result == null)
                                {
                                    throw new InvalidOperationException("No child result present");
                                }
                                handle.CompletionSource.SetResult(completeRes.Completed.Result);
                                break;
                            case ChildWorkflowResult.StatusOneofCase.Failed:
                                handle.CompletionSource.SetException(instance.failureConverter.ToException(
                                    completeRes.Failed.Failure_, instance.PayloadConverter));
                                break;
                            case ChildWorkflowResult.StatusOneofCase.Cancelled:
                                handle.CompletionSource.SetException(instance.failureConverter.ToException(
                                    completeRes.Cancelled.Failure, instance.PayloadConverter));
                                break;
                            default:
                                throw new InvalidOperationException("Unrecognized child complete case");
                        }
                    }
                });
                return handleSource.Task;
            }

            private Task SignalExternalWorkflowInternalAsync(
                SignalExternalWorkflowExecution cmd, CancellationToken? inputCancelToken)
            {
                var token = inputCancelToken ?? instance.CancellationToken;
                // Like other cases (e.g. child workflow start), we do not even want to schedule if
                // the cancellation token is already cancelled.
                if (token.IsCancellationRequested)
                {
                    return Task.FromException(
                        new CanceledFailureException("Signal cancelled before scheduled"));
                }

                var source = new TaskCompletionSource<ResolveSignalExternalWorkflow>();
                instance.externalSignalsPending[cmd.Seq] = source;
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
                        var res = await source.Task.ConfigureAwait(true);
                        instance.externalSignalsPending.Remove(cmd.Seq);
                        // Throw if failed
                        if (res.Failure != null)
                        {
                            throw instance.failureConverter.ToException(
                                res.Failure, instance.PayloadConverter);
                        }
                    }
                });
            }

            private Task<TResult> ExecuteActivityInternalAsync<TResult>(
                bool local,
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
                var source = new TaskCompletionSource<ActivityResolution>();
                instance.activitiesPending[seq] = source;
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
                                if (local)
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
                            res = await source.Task.ConfigureAwait(true);
                        }

                        // Apply result. Only DoBackoff will cause loop to continue.
                        instance.activitiesPending.Remove(seq);
                        switch (res.StatusCase)
                        {
                            case ActivityResolution.StatusOneofCase.Completed:
                                // Ignore result if they didn't want it
                                if (typeof(TResult) == typeof(ValueTuple))
                                {
                                    return default!;
                                }
                                // Otherwise we expect a single payload
                                if (res.Completed.Result == null)
                                {
                                    throw new InvalidOperationException("No activity result present");
                                }
                                return instance.PayloadConverter.ToValue<TResult>(res.Completed.Result);
                            case ActivityResolution.StatusOneofCase.Failed:
                                throw instance.failureConverter.ToException(
                                    res.Failed.Failure_, instance.PayloadConverter);
                            case ActivityResolution.StatusOneofCase.Cancelled:
                                throw instance.failureConverter.ToException(
                                    res.Cancelled.Failure, instance.PayloadConverter);
                            case ActivityResolution.StatusOneofCase.Backoff:
                                // We have to sleep the backoff amount. Note, this can be cancelled
                                // like any other timer.
                                await instance.DelayAsync(
                                    res.Backoff.BackoffDuration.ToTimeSpan(),
                                    cancellationToken).ConfigureAwait(true);
                                // Re-schedule with backoff info
                                seq = applyScheduleCommand(res.Backoff);
                                source = new TaskCompletionSource<ActivityResolution>();
                                instance.activitiesPending[seq] = source;
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
            private readonly string id;
            private readonly string firstExecutionRunId;

            /// <summary>
            /// Initializes a new instance of the <see cref="ChildWorkflowHandleImpl{TWorkflow, TResult}"/> class.
            /// </summary>
            /// <param name="instance">Workflow instance.</param>
            /// <param name="id">Workflow ID.</param>
            /// <param name="firstExecutionRunId">Workflow run ID.</param>
            public ChildWorkflowHandleImpl(
                WorkflowInstance instance, string id, string firstExecutionRunId)
            {
                this.instance = instance;
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
            internal TaskCompletionSource<Payload> CompletionSource { get; } = new();

            /// <inheritdoc />
            public override async Task<TLocalResult> GetResultAsync<TLocalResult>()
            {
                var payload = await CompletionSource.Task.ConfigureAwait(true);
                // Ignore if they are ignoring result
                if (typeof(TLocalResult) == typeof(ValueTuple))
                {
                    return default!;
                }
                return instance.PayloadConverter.ToValue<TLocalResult>(payload);
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
    }
}