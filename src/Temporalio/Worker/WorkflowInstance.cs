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
        private readonly LinkedList<Task> scheduledTasks = new();
        private readonly Dictionary<Task, LinkedListNode<Task>> scheduledTaskNodes = new();
        private readonly Dictionary<uint, TaskCompletionSource<object?>> timersPending = new();
        private readonly Dictionary<string, List<SignalWorkflow>> bufferedSignals = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly LinkedList<Tuple<Func<bool>, TaskCompletionSource<object?>>> conditions = new();
        private WorkflowActivationCompletion? completion;
        // Will be set to null after last use (i.e. when workflow actually started)
        private Lazy<object?[]>? startArgs;
        private object? instance;
        private Exception? currentActivationException;
        private uint timerCounter;

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
            var act = details.InitialActivation;
            var start = details.Start;
            startArgs = new(
                () => DecodeArgs(defn.RunMethod, start.Arguments, $"Workflow {start.WorkflowType}"),
                false);
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
                RawMemo: start.Memo,
                RawSearchAttributes: start.SearchAttributes,
                RetryPolicy: start.RetryPolicy == null ? null : RetryPolicy.FromProto(start.RetryPolicy),
                RunID: act.RunId,
                RunTimeout: start.WorkflowRunTimeout?.ToTimeSpan(),
                StartTime: act.Timestamp.ToDateTime(),
                TaskQueue: details.TaskQueue,
                TaskTimeout: start.WorkflowTaskTimeout.ToTimeSpan(),
                WorkflowID: start.WorkflowId,
                WorkflowType: start.WorkflowType);
            Logger = loggerFactory.CreateLogger($"Temporalio.Workflow:{start.WorkflowType}");
            // We accept overflowing for seed (uint64 -> int32)
            Random = new(unchecked((int)details.Start.RandomnessSeed));
            TaskTracingEnabled = !details.DisableTaskTracing;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="WorkflowInstance"/> class.
        /// </summary>
        ~WorkflowInstance() => cancellationTokenSource.Dispose();

        /// <inheritdoc/>
        public override int MaximumConcurrencyLevel => 1;

        /// <summary>
        /// Gets a value indicating whether this workflow works with task tracing.
        /// </summary>
        public bool TaskTracingEnabled { get; private init; }

        /// <inheritdoc />
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        /// <inheritdoc />
        public WorkflowInfo Info { get; private init; }

        /// <inheritdoc />
        public bool IsReplaying { get; private set; }

        /// <inheritdoc />
        public IDictionary<string, WorkflowQueryDefinition> Queries => mutableQueries.Value;

        /// <inheritdoc />
        public Random Random { get; private set; }

        /// <inheritdoc />
        public IDictionary<string, WorkflowSignalDefinition> Signals => mutableSignals.Value;

        /// <inheritdoc />
        public DateTime UtcNow { get; private set; }

        /// <summary>
        /// Gets the workflow logger.
        /// </summary>
        internal ILogger Logger { get; private init; }

        /// <summary>
        /// Gets the instance, lazily creating if needed. This should never be called outside this
        /// scheduler.
        /// </summary>
        private object Instance
        {
            get
            {
                if (instance == null)
                {
                    // If there is a constructor accepting arguments, use that
                    if (defn.InitConstructor != null)
                    {
                        instance = defn.InitConstructor.Invoke(startArgs!.Value);
                    }
                    else
                    {
                        instance = Activator.CreateInstance(defn.Type)!;
                    }
                }
                return instance;
            }
        }

        /// <inheritdoc/>
        public Task DelayAsync(TimeSpan delay, CancellationToken? cancellationToken) =>
            outbound.Value.DelayAsync(new(Delay: delay, CancellationToken: cancellationToken));

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
            using (Logger.BeginScope(Info.LoggerScope))
            {
                completion = new() { RunId = act.RunId, Successful = new() };
                currentActivationException = null;
                IsReplaying = act.IsReplaying;
                UtcNow = act.Timestamp.ToDateTime();
                // We need to sort jobs by notify, then signal, then non-query, then query
                var jobs = act.Jobs.OrderBy(job =>
                {
                    switch (job.VariantCase)
                    {
                        case WorkflowActivationJob.VariantOneofCase.NotifyHasPatch:
                            return 0;
                        case WorkflowActivationJob.VariantOneofCase.SignalWorkflow:
                            return 1;
                        case WorkflowActivationJob.VariantOneofCase.QueryWorkflow:
                            return 3;
                        default:
                            return 2;
                    }
                });

                // Run the event loop until yielded for each job
                try
                {
                    var previousContext = SynchronizationContext.Current;
                    try
                    {
                        // We must set the sync context to null so work isn't posted there
                        SynchronizationContext.SetSynchronizationContext(null);
                        foreach (var job in jobs)
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
                    Logger.LogWarning(
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
                        Logger.LogError(
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
                        Logger.LogWarning("Task unexpectedly was unable to execute");
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
        private Task QueueNewTaskAsync(Func<Task> func) => taskFactory.StartNew(func).Unwrap();

        private Task<T> QueueNewTaskAsync<T>(Func<Task<T>> func) => taskFactory.StartNew(func).Unwrap();
#pragma warning restore CA2008

        private async Task RunTopLevelAsync(Func<Task> func)
        {
            try
            {
                try
                {
                    await func().ConfigureAwait(true);
                }
                catch (ContinueAsNewException)
                {
                    Logger.LogDebug("Workflow requested continue as new with run ID {RunID}", Info.RunID);
                    // TODO(cretz): This
                    throw new NotImplementedException();
                }
                catch (Exception e) when (
                    CancellationToken.IsCancellationRequested && (e is OperationCanceledException ||
                        e is CancelledFailureException ||
                        (e as ActivityFailureException)?.InnerException is CancelledFailureException ||
                        (e as ChildWorkflowFailureException)?.InnerException is CancelledFailureException))
                {
                    // If cancel was ever requested and this is a cancellation or an activity/child
                    // cancellation, we add a cancel command. Technically this means that a
                    // swallowed cancel followed by, say, an activity cancel later on will show the
                    // workflow as cancelled. But this is a Temporal limitation in that cancellation
                    // is a state not an event.
                    Logger.LogDebug(e, "Workflow raised cancel with run ID {RunID}", Info.RunID);
                    AddCommand(new() { CancelWorkflowExecution = new() });
                }
                catch (Exception e) when (e is FailureException || e is OperationCanceledException)
                {
                    // Failure exceptions fail the workflow. We let this failure conversion throw if
                    // it cannot convert the failure. We also allow non-internally-caught
                    // cancellation exceptions fail the workflow because it's clearer when users are
                    // reusing cancellation tokens if the workflow fails.
                    Logger.LogDebug(e, "Workflow raised failure with run ID {RunID}", Info.RunID);
                    var failure = failureConverter.ToFailure(e, payloadConverter);
                    AddCommand(new() { FailWorkflowExecution = new() { Failure = failure } });
                }
            }
            catch (Exception e)
            {
                Logger.LogDebug(e, "Workflow raised unexpected failure with run ID {RunID}", Info.RunID);
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
                case WorkflowActivationJob.VariantOneofCase.QueryWorkflow:
                    ApplyQueryWorkflow(job.QueryWorkflow);
                    break;
                case WorkflowActivationJob.VariantOneofCase.RemoveFromCache:
                    // Ignore, handled outside the instance
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

        private void ApplyCancelWorkflow()
        {
            cancellationTokenSource.Cancel();
        }

        private void ApplyFireTimer(FireTimer fireTimer)
        {
            if (timersPending.TryGetValue(fireTimer.Seq, out var source))
            {
                timersPending.Remove(fireTimer.Seq);
                source.TrySetResult(null);
            }
        }

        private void ApplyQueryWorkflow(QueryWorkflow query)
        {
            // Queue it up so it can run in workflow environment
            _ = QueueNewTaskAsync(() =>
            {
                var origCmdCount = completion?.Successful?.Commands?.Count;
                try
                {
                    // Find definition or fail
                    var queries = mutableQueries.IsValueCreated ? mutableQueries.Value : defn.Queries;
                    if (!queries.TryGetValue(query.QueryType, out var queryDefn))
                    {
                        var knownQueries = queries.Keys.OrderBy(k => k);
                        throw new InvalidOperationException(
                            $"Query handler for {query.QueryType} expected but not found, " +
                            $"known queries: [{string.Join(" ", knownQueries)}]");
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
                    throw new ArgumentException("Delay duration cannot be less than 0.");
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
                        // Try cancel, then only remove timer and send cancel if seq not 0
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
        }
    }
}