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
        private readonly TaskFactory taskFactory;
        private readonly WorkflowDefinition defn;
        private readonly IPayloadConverter payloadConverter;
        private readonly IFailureConverter failureConverter;
        private readonly Lazy<WorkflowInboundInterceptor> inbound;
        private readonly Lazy<WorkflowOutboundInterceptor> outbound;
        // Lazily created if asked for by user
        private readonly Lazy<NotifyOnSetDictionary<string, WorkflowQueryDefinition>> mutableQueries;
        // Lazily created if asked for by user
        private readonly Lazy<NotifyOnSetDictionary<string, WorkflowSignalDefinition>> mutableSignals;
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
        private readonly Dictionary<string, List<SignalWorkflow>> bufferedSignals = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly LinkedList<Tuple<Func<bool>, TaskCompletionSource<object?>>> conditions = new();
        private readonly HashSet<string> patchesNotified = new();
        private readonly Dictionary<string, bool> patchesMemoized = new();
        private readonly WorkflowStackTrace workflowStackTrace;
        // Only non-null if stack trace is not None
        private readonly LinkedList<System.Diagnostics.StackTrace>? pendingTaskStackTraces;
        private readonly ILogger logger;
        private readonly ReplaySafeLogger replaySafeLogger;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowInstance"/> class.
        /// </summary>
        /// <param name="details">Immutable details about the instance.</param>
        /// <param name="loggerFactory">Logger factory to use.</param>
        public WorkflowInstance(WorkflowInstanceDetails details, ILoggerFactory loggerFactory)
        {
            taskFactory = new(default, TaskCreationOptions.None, TaskContinuationOptions.ExecuteSynchronously, this);
            defn = details.Definition;
            payloadConverter = details.PayloadConverter;
            failureConverter = details.FailureConverter;
            var rootInbound = new InboundImpl(this);
            inbound = new(
                () =>
                {
                    var ret = details.InboundInterceptorTypes.Reverse().Aggregate(
                        (WorkflowInboundInterceptor)rootInbound,
                        (v, type) => (WorkflowInboundInterceptor)Activator.CreateInstance(type, v)!);
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
            mutableQueries = new(() => new(defn.Queries, OnQueryDefinitionAdded), false);
            mutableSignals = new(() => new(defn.Signals, OnSignalDefinitionAdded), false);
            var initialMemo = details.Start.Memo;
            memo = new(
                () => initialMemo == null ? new Dictionary<string, IRawValue>(0) :
                    initialMemo.Fields.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (IRawValue)new RawValue(payloadConverter, kvp.Value)),
                false);
            var initialSearchAttributes = details.Start.SearchAttributes;
            typedSearchAttributes = new(
                () => initialSearchAttributes == null ? new(new()) :
                    SearchAttributeCollection.FromProto(initialSearchAttributes),
                false);
            var act = details.InitialActivation;
            var start = details.Start;
            startArgs = new(
                () => DecodeArgs(defn.RunMethod, start.Arguments, $"Workflow {start.WorkflowType}"),
                false);
            initialSearchAttributes = details.Start.SearchAttributes;
            WorkflowInfo.ParentInfo? parent = null;
            if (start.ParentWorkflowInfo != null)
            {
                parent = new(
                    Namespace: start.ParentWorkflowInfo.Namespace,
                    RunID: start.ParentWorkflowInfo.RunId,
                    WorkflowID: start.ParentWorkflowInfo.WorkflowId);
            }
            static string? NonEmptyOrNull(string s) => string.IsNullOrEmpty(s) ? null : s;
            Info = new(
                Attempt: start.Attempt,
                ContinuedRunID: NonEmptyOrNull(start.ContinuedFromExecutionRunId),
                CronSchedule: NonEmptyOrNull(start.CronSchedule),
                ExecutionTimeout: start.WorkflowExecutionTimeout?.ToTimeSpan(),
                Namespace: details.Namespace,
                Parent: parent,
                RetryPolicy: start.RetryPolicy == null ? null : RetryPolicy.FromProto(start.RetryPolicy),
                RunID: act.RunId,
                RunTimeout: start.WorkflowRunTimeout?.ToTimeSpan(),
                StartTime: act.Timestamp.ToDateTime(),
                TaskQueue: details.TaskQueue,
                TaskTimeout: start.WorkflowTaskTimeout.ToTimeSpan(),
                WorkflowID: start.WorkflowId,
                WorkflowType: start.WorkflowType);
            workflowStackTrace = details.WorkflowStackTrace;
            pendingTaskStackTraces = workflowStackTrace == WorkflowStackTrace.None ? null : new();
            logger = loggerFactory.CreateLogger($"Temporalio.Workflow:{start.WorkflowType}");
            replaySafeLogger = new(logger);
            // We accept overflowing for seed (uint64 -> int32)
            Random = new(unchecked((int)details.Start.RandomnessSeed));
            TracingEventsEnabled = !details.DisableTracingEvents;
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
        public WorkflowInfo Info { get; private init; }

        /// <inheritdoc />
        public bool IsReplaying { get; private set; }

        /// <inheritdoc />
        public ILogger Logger => replaySafeLogger;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IRawValue> Memo => memo.Value;

        /// <inheritdoc />
        public IDictionary<string, WorkflowQueryDefinition> Queries => mutableQueries.Value;

        /// <inheritdoc />
        public Random Random { get; private set; }

        /// <inheritdoc />
        public IDictionary<string, WorkflowSignalDefinition> Signals => mutableSignals.Value;

        /// <inheritdoc />
        public SearchAttributeCollection TypedSearchAttributes => typedSearchAttributes.Value;

        /// <inheritdoc />
        public DateTime UtcNow { get; private set; }

        /// <summary>
        /// Gets the instance, lazily creating if needed. This should never be called outside this
        /// scheduler.
        /// </summary>
        private object Instance
        {
            get
            {
                // We create this lazily because we want the constructor in a workflow context
                instance ??= defn.CreateWorkflowInstance(startArgs!.Value);
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
        public ExternalWorkflowHandle GetExternalWorkflowHandle(string id, string? runID = null) =>
            new ExternalWorkflowHandleImpl(this, id, runID);

        /// <inheritdoc />
        public bool Patch(string patchID, bool deprecated)
        {
            // Use memoized result if present. If this is being deprecated, we can still use
            // memoized result and skip the command.
            if (patchesMemoized.TryGetValue(patchID, out var patched))
            {
                return patched;
            }
            patched = !IsReplaying || patchesNotified.Contains(patchID);
            patchesMemoized[patchID] = patched;
            if (patched)
            {
                AddCommand(new()
                {
                    SetPatchMarker = new() { PatchId = patchID, Deprecated = deprecated },
                });
            }
            return patched;
        }

        /// <inheritdoc/>
        public Task<ChildWorkflowHandle<TResult>> StartChildWorkflowAsync<TResult>(
            string workflow, IReadOnlyCollection<object?> args, ChildWorkflowOptions options) =>
            outbound.Value.StartChildWorkflowAsync<TResult>(
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
                    upsertedMemo.Fields[update.UntypedKey] = payloadConverter.ToPayload(
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
                    memo.Value[update.UntypedKey] = new RawValue(
                        payloadConverter, upsertedMemo.Fields[update.UntypedKey]);
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
                IsReplaying = act.IsReplaying;
                UtcNow = act.Timestamp.ToDateTime();

                // Run the event loop until yielded for each job
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
                    logger.LogWarning(
                        e,
                        "Failed activation on workflow {WorkflowType} with ID {WorkflowID} and run ID {RunID}",
                        Info.WorkflowType,
                        Info.WorkflowID,
                        Info.RunID);
                    try
                    {
                        completion.Failed = new()
                        {
                            Failure_ = failureConverter.ToFailure(e, payloadConverter),
                        };
                    }
                    catch (Exception inner)
                    {
                        logger.LogError(
                            inner,
                            "Failed converting activation exception on workflow with run ID {RunID}",
                            Info.RunID);
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
            // TODO(cretz): Fix to run as long as any tasks not yielded on Temporal
            while (scheduledTasks.Any())
            {
                while (scheduledTasks.Any())
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

                // Collect all condition sources to mark complete and then complete them. This
                // avoids modify-during-iterate issues.
                if (checkConditions)
                {
                    var completeConditions = conditions.Where(tuple => tuple.Item1());
                    foreach (var source in conditions.Where(t => t.Item1()).Select(t => t.Item2))
                    {
                        source.TrySetResult(null);
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
                    logger.LogDebug("Workflow requested continue as new with run ID {RunID}", Info.RunID);
                    var cmd = new ContinueAsNewWorkflowExecution()
                    {
                        WorkflowType = e.Input.Workflow,
                        TaskQueue = e.Input.Options?.TaskQueue ?? string.Empty,
                        Arguments = { payloadConverter.ToPayloads(e.Input.Args) },
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
                            kvp => kvp.Key, kvp => payloadConverter.ToPayload(kvp.Value)));
                    }
                    if (e.Input.Options?.TypedSearchAttributes is SearchAttributeCollection attrs)
                    {
                        cmd.SearchAttributes.Add(attrs.ToProto().IndexedFields);
                    }
                    if (e.Input.Headers is IDictionary<string, Payload> headers)
                    {
                        cmd.Headers.Add(headers);
                    }
                    AddCommand(new() { ContinueAsNewWorkflowExecution = cmd });
                }
                catch (Exception e) when (
                    CancellationToken.IsCancellationRequested && TemporalException.IsCancelledException(e))
                {
                    // If cancel was ever requested and this is a cancellation or an activity/child
                    // cancellation, we add a cancel command. Technically this means that a
                    // swallowed cancel followed by, say, an activity cancel later on will show the
                    // workflow as cancelled. But this is a Temporal limitation in that cancellation
                    // is a state not an event.
                    logger.LogDebug(e, "Workflow raised cancel with run ID {RunID}", Info.RunID);
                    AddCommand(new() { CancelWorkflowExecution = new() });
                }
                catch (Exception e) when (e is FailureException || e is OperationCanceledException)
                {
                    // Failure exceptions fail the workflow. We let this failure conversion throw if
                    // it cannot convert the failure. We also allow non-internally-caught
                    // cancellation exceptions fail the workflow because it's clearer when users are
                    // reusing cancellation tokens if the workflow fails.
                    logger.LogDebug(e, "Workflow raised failure with run ID {RunID}", Info.RunID);
                    var failure = failureConverter.ToFailure(e, payloadConverter);
                    AddCommand(new() { FailWorkflowExecution = new() { Failure = failure } });
                }
            }
            catch (Exception e)
            {
                logger.LogDebug(e, "Workflow raised unexpected failure with run ID {RunID}", Info.RunID);
                // All exceptions this far fail the task
                currentActivationException = e;
            }
        }

        private void Apply(WorkflowActivationJob job)
        {
            switch (job.VariantCase)
            {
                case WorkflowActivationJob.VariantOneofCase.CancelWorkflow:
                    // TODO(cretz): Do we care about "details" on the object?
                    ApplyCancelWorkflow();
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
                        var queries = mutableQueries.IsValueCreated ? mutableQueries.Value : defn.Queries;
                        if (!queries.TryGetValue(query.QueryType, out queryDefn))
                        {
                            var knownQueries = queries.Keys.OrderBy(k => k);
                            throw new InvalidOperationException(
                                $"Query handler for {query.QueryType} expected but not found, " +
                                $"known queries: [{string.Join(" ", knownQueries)}]");
                        }
                    }
                    var resultObj = inbound.Value.HandleQuery(new(
                            ID: query.QueryId,
                            Query: query.QueryType,
                            Definition: queryDefn,
                            Args: DecodeArgs(
                                queryDefn.Method ?? queryDefn.Delegate!.Method,
                                query.Arguments,
                                $"Query {query.QueryType}"),
                            Headers: query.Headers));
                    AddCommand(new()
                    {
                        RespondToQuery = new()
                        {
                            QueryId = query.QueryId,
                            Succeeded = new() { Response = payloadConverter.ToPayload(resultObj) },
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
                            Failed = failureConverter.ToFailure(e, payloadConverter),
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
            var signals = mutableSignals.IsValueCreated ? mutableSignals.Value : defn.Signals;
            if (!signals.TryGetValue(signal.SignalName, out var signalDefn))
            {
                if (!bufferedSignals.TryGetValue(signal.SignalName, out var signalList))
                {
                    signalList = new();
                    bufferedSignals[signal.SignalName] = signalList;
                }
                signalList.Add(signal);
                return;
            }

            // Run the handler as a top-level function
            _ = QueueNewTaskAsync(() => RunTopLevelAsync(async () =>
            {
                await inbound.Value.HandleSignalAsync(new(
                    Signal: signal.SignalName,
                    Definition: signalDefn,
                    Args: DecodeArgs(
                        signalDefn.Method ?? signalDefn.Delegate!.Method,
                        signal.Input,
                        $"Signal {signal.SignalName}"),
                    Headers: signal.Headers)).ConfigureAwait(true);
            }));
        }

        private void ApplyStartWorkflow(StartWorkflow start)
        {
            _ = QueueNewTaskAsync(() => RunTopLevelAsync(async () =>
            {
                var input = new ExecuteWorkflowInput(
                    Instance: Instance,
                    RunMethod: defn.RunMethod,
                    Args: startArgs!.Value,
                    Headers: start.Headers);
                // We no longer need start args after this point, so we are unsetting them
                startArgs = null;
                var resultObj = await inbound.Value.ExecuteWorkflowAsync(input).ConfigureAwait(true);
                var result = payloadConverter.ToPayload(resultObj);
                AddCommand(new() { CompleteWorkflowExecution = new() { Result = result } });
            }));
        }

        private void ApplyUpdateRandomSeed(UpdateRandomSeed update) =>
            Random = new(unchecked((int)update.RandomnessSeed));

        private void OnQueryDefinitionAdded(string name, WorkflowQueryDefinition defn)
        {
            if (name != defn.Name)
            {
                throw new ArgumentException($"Query name {name} doesn't match definition name {defn.Name}");
            }
        }

        private void OnSignalDefinitionAdded(string name, WorkflowSignalDefinition defn)
        {
            if (name != defn.Name)
            {
                throw new ArgumentException($"Signal name {name} doesn't match definition name {defn.Name}");
            }
            // Send all buffered signals
            if (bufferedSignals.TryGetValue(name, out var buffered))
            {
                bufferedSignals.Remove(name);
                buffered.ForEach(ApplySignalWorkflow);
            }
        }

        private object?[] DecodeArgs(
            MethodInfo method, IReadOnlyCollection<Payload> payloads, string itemName)
        {
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
                        payloadConverter.ToValue(payload, paramInfo.ParameterType)));
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
                    new[] { "\r", "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

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
                        WorkflowId = input.ID,
                        RunId = input.RunID ?? string.Empty,
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
                            res.Failure, instance.payloadConverter);
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
                            ActivityId = input.Options.ActivityID ?? seq.ToString(),
                            ActivityType = input.Activity,
                            TaskQueue = input.Options.TaskQueue ?? instance.Info.TaskQueue,
                            Arguments = { instance.payloadConverter.ToPayloads(input.Args) },
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
                            ActivityId = input.Options.ActivityID ?? seq.ToString(),
                            ActivityType = input.Activity,
                            Arguments = { instance.payloadConverter.ToPayloads(input.Args) },
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
                    ChildWorkflowId = input.ID,
                    SignalName = input.Signal,
                    Args = { instance.payloadConverter.ToPayloads(input.Args) },
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
                        WorkflowId = input.ID,
                        RunId = input.RunID ?? string.Empty,
                    },
                    SignalName = input.Signal,
                    Args = { instance.payloadConverter.ToPayloads(input.Args) },
                };
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
                return SignalExternalWorkflowInternalAsync(cmd, input.Options?.CancellationToken);
            }

            /// <inheritdoc />
            public override Task<ChildWorkflowHandle<TResult>> StartChildWorkflowAsync<TResult>(
                StartChildWorkflowInput input)
            {
                var token = input.Options.CancellationToken ?? instance.CancellationToken;
                // We do not even want to schedule if the cancellation token is already cancelled.
                // We choose to use cancelled failure instead of wrapping in child failure which is
                // similar to what Java and TypeScript do, with the accepted tradeoff that it makes
                // catch clauses more difficult (hence the presence of
                // TemporalException.IsCancelledException helper).
                if (token.IsCancellationRequested)
                {
                    return Task.FromException<ChildWorkflowHandle<TResult>>(
                        new CancelledFailureException("Child cancelled before scheduled"));
                }

                // Add the start command
                var seq = ++instance.childWorkflowCounter;
                var cmd = new StartChildWorkflowExecution()
                {
                    Seq = seq,
                    Namespace = instance.Info.Namespace,
                    WorkflowId = input.Options.ID ?? Workflow.NewGuid().ToString(),
                    WorkflowType = input.Workflow,
                    TaskQueue = input.Options.TaskQueue ?? instance.Info.TaskQueue,
                    Input = { instance.payloadConverter.ToPayloads(input.Args) },
                    ParentClosePolicy = (Bridge.Api.ChildWorkflow.ParentClosePolicy)input.Options.ParentClosePolicy,
                    WorkflowIdReusePolicy = input.Options.IDReusePolicy,
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
                        kvp => kvp.Key, kvp => instance.payloadConverter.ToPayload(kvp.Value)));
                }
                if (input.Options?.TypedSearchAttributes is SearchAttributeCollection attrs)
                {
                    cmd.SearchAttributes.Add(attrs.ToProto().IndexedFields);
                }
                if (input.Headers is IDictionary<string, Payload> headers)
                {
                    cmd.Headers.Add(headers);
                }
                instance.AddCommand(new() { StartChildWorkflowExecution = cmd });

                // Add start as pending and wait inside of task
                var handleSource = new TaskCompletionSource<ChildWorkflowHandle<TResult>>();
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
                        ChildWorkflowHandleImpl<TResult> handle;
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
                                        startRes.Cancelled.Failure, instance.payloadConverter));
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
                                    completeRes.Failed.Failure_, instance.payloadConverter));
                                break;
                            case ChildWorkflowResult.StatusOneofCase.Cancelled:
                                handle.CompletionSource.SetException(instance.failureConverter.ToException(
                                    completeRes.Cancelled.Failure, instance.payloadConverter));
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
                        new CancelledFailureException("Signal cancelled before scheduled"));
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
                                res.Failure, instance.payloadConverter);
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
                // TemporalException.IsCancelledException helper).
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromException<TResult>(
                        new CancelledFailureException("Activity cancelled before scheduled"));
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
                                return instance.payloadConverter.ToValue<TResult>(res.Completed.Result);
                            case ActivityResolution.StatusOneofCase.Failed:
                                throw instance.failureConverter.ToException(
                                    res.Failed.Failure_, instance.payloadConverter);
                            case ActivityResolution.StatusOneofCase.Cancelled:
                                throw instance.failureConverter.ToException(
                                    res.Cancelled.Failure, instance.payloadConverter);
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
        /// <typeparam name="TResult">Child workflow result.</typeparam>
        internal class ChildWorkflowHandleImpl<TResult> : ChildWorkflowHandle<TResult>
        {
            private readonly WorkflowInstance instance;
            private readonly string id;
            private readonly string firstExecutionRunID;

            /// <summary>
            /// Initializes a new instance of the <see cref="ChildWorkflowHandleImpl{TResult}"/> class.
            /// </summary>
            /// <param name="instance">Workflow instance.</param>
            /// <param name="id">Workflow ID.</param>
            /// <param name="firstExecutionRunID">Workflow run ID.</param>
            public ChildWorkflowHandleImpl(
                WorkflowInstance instance, string id, string firstExecutionRunID)
            {
                this.instance = instance;
                this.id = id;
                this.firstExecutionRunID = firstExecutionRunID;
            }

            /// <inheritdoc />
            public override string ID => id;

            /// <inheritdoc />
            public override string FirstExecutionRunID => firstExecutionRunID;

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
                return instance.payloadConverter.ToValue<TLocalResult>(payload);
            }

            /// <inheritdoc />
            public override Task SignalAsync(
                string signal,
                IReadOnlyCollection<object?> args,
                ChildWorkflowSignalOptions? options = null) =>
                instance.outbound.Value.SignalChildWorkflowAsync(new(
                    ID: ID,
                    Signal: signal,
                    Args: args,
                    Options: options,
                    Headers: null));
        }

        /// <summary>
        /// External workflow handle implementation.
        /// </summary>
        internal class ExternalWorkflowHandleImpl : ExternalWorkflowHandle
        {
            private readonly WorkflowInstance instance;
            private readonly string id;
            private readonly string? runID;

            /// <summary>
            /// Initializes a new instance of the <see cref="ExternalWorkflowHandleImpl"/> class.
            /// </summary>
            /// <param name="instance">Workflow instance.</param>
            /// <param name="id">Workflow ID.</param>
            /// <param name="runID">Workflow run ID.</param>
            public ExternalWorkflowHandleImpl(WorkflowInstance instance, string id, string? runID)
            {
                this.instance = instance;
                this.id = id;
                this.runID = runID;
            }

            /// <inheritdoc />
            public override string ID => id;

            /// <inheritdoc />
            public override string? RunID => runID;

            /// <inheritdoc />
            public override Task SignalAsync(
                string signal,
                IReadOnlyCollection<object?> args,
                ExternalWorkflowSignalOptions? options = null) =>
                instance.outbound.Value.SignalExternalWorkflowAsync(new(
                    ID: ID,
                    RunID: RunID,
                    Signal: signal,
                    Args: args,
                    Options: options,
                    Headers: null));

            /// <inheritdoc />
            public override Task CancelAsync() =>
                instance.outbound.Value.CancelExternalWorkflowAsync(new(ID: ID, RunID: RunID));
        }
    }
}