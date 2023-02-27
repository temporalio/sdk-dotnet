#pragma warning disable CA1031 // We do want to catch _all_ exceptions in this file sometimes

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Bridge.Api.WorkflowActivation;
using Temporalio.Bridge.Api.WorkflowCommands;
using Temporalio.Bridge.Api.WorkflowCompletion;
using Temporalio.Exceptions;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;

namespace Temporalio.Worker
{
    /// <summary>
    /// Instance of a workflow execution.
    /// </summary>
    internal class WorkflowInstance : TaskScheduler, IWorkflowInstance
    {
        private readonly TaskFactory taskFactory;
        private readonly WorkflowInstanceDetails details;
        private readonly LinkedList<Task> scheduledTasks = new();
        private readonly Dictionary<Task, LinkedListNode<Task>> scheduledTaskNodes = new();
        private WorkflowActivationCompletion? completion;
        private object?[]? startParameters;
        private object? instance;
        private InboundImpl rootInbound = new();
        private WorkflowInboundInterceptor? inbound;
        private WorkflowOutboundInterceptor? outbound;
        private bool cancelRequested;
        private Exception? currentActivationException;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowInstance"/> class.
        /// </summary>
        /// <param name="details">Immutable details about the instance.</param>
        /// <param name="loggerFactory">Logger factory to use.</param>
        public WorkflowInstance(WorkflowInstanceDetails details, ILoggerFactory loggerFactory)
        {
            taskFactory = new(default, TaskCreationOptions.None, TaskContinuationOptions.ExecuteSynchronously, this);
            this.details = details;
            var act = details.InitialActivation;
            var start = details.Start;
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
        }

        /// <inheritdoc/>
        public override int MaximumConcurrencyLevel => 1;

        /// <summary>
        /// Gets a value indicating whether this workflow works with task tracing.
        /// </summary>
        public bool TaskTracingEnabled => !details.DisableTaskTracing;

        /// <summary>
        /// Gets the workflow info.
        /// </summary>
        internal WorkflowInfo Info { get; private init; }

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
                    if (details.Definition.InitConstructor != null)
                    {
                        instance = details.Definition.InitConstructor.Invoke(StartParameters);
                    }
                    else
                    {
                        instance = Activator.CreateInstance(details.Definition.Type)!;
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// Gets the start parameters, lazily creating if needed. This should never be called
        /// outside this scheduler.
        /// </summary>
        private object?[] StartParameters
        {
            get
            {
                if (startParameters == null)
                {
                    var paramInfos = details.Definition.RunMethod.GetParameters();
                    if (details.Start.Arguments.Count < paramInfos.Length &&
                        !paramInfos[details.Start.Arguments.Count].HasDefaultValue)
                    {
                        throw new InvalidOperationException(
                            $"Workflow {Info.WorkflowType} given {details.Start.Arguments.Count} parameter(s)," +
                            " but more than that are required by the signature");
                    }
                    // Zip the params and input and then decode each. It is intentional that we discard
                    // extra input arguments that the signature doesn't accept.
                    var paramVals = new List<object?>(paramInfos.Length);
                    try
                    {
                        paramVals.AddRange(
                            details.Start.Arguments.Zip(paramInfos, (payload, paramInfo) =>
                                details.PayloadConverter.ToValue(payload, paramInfo.ParameterType)));
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException(
                            $"Failed decoding parameters for workflow {Info.WorkflowType}", e);
                    }
                    // Append default parameters if needed
                    for (var i = details.Start.Arguments.Count; i < paramInfos.Length; i++)
                    {
                        paramVals.Add(paramInfos[i].DefaultValue);
                    }
                    startParameters = paramVals.ToArray();
                }
                return startParameters;
            }
        }

        private WorkflowInboundInterceptor Inbound
        {
            get
            {
                inbound ??= details.InboundInterceptorTypes.Reverse().Aggregate(
                        (WorkflowInboundInterceptor)rootInbound,
                        (v, type) => (WorkflowInboundInterceptor)Activator.CreateInstance(type, v)!);
                return inbound;
            }
        }

        private WorkflowOutboundInterceptor Outbound
        {
            get
            {
                if (outbound == null)
                {
                    _ = Inbound;
                    outbound = rootInbound.Outbound!;
                }
                return outbound;
            }
        }

        /// <inheritdoc/>
        public WorkflowActivationCompletion Activate(WorkflowActivation act)
        {
            using (Logger.BeginScope(Info.LoggerScope))
            {
                completion = new() { RunId = act.RunId, Successful = new() };
                currentActivationException = null;
                // We need to sort jobs
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
                            Failure_ = details.FailureConverter.ToFailure(e, details.PayloadConverter),
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
                return completion;
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

                // TODO(cretz): Check conditions
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

        private void QueueNewTask(Func<Task> func)
        {
#pragma warning disable CA2008 // We don't have to pass a scheduler, factory already implies one
            _ = taskFactory.StartNew(func).Unwrap();
#pragma warning restore CA2008
        }

        private async Task RunTopLevelAsync(Func<Task> func)
        {
            try
            {
                try
                {
                    await func().ConfigureAwait(false);
                }
                catch (ContinueAsNewException)
                {
                    Logger.LogDebug("Workflow requested continue as new with run ID {RunID}", Info.RunID);
                    // TODO(cretz): This
                    throw new NotImplementedException();
                }
                catch (Exception e) when (
                    cancelRequested && (e is OperationCanceledException ||
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
                catch (FailureException e)
                {
                    // Failure exceptions fail the workflow. We let this failure conversion throw if
                    // it cannot convert the failure.
                    Logger.LogDebug(e, "Workflow raised failure with run ID {RunID}", Info.RunID);
                    var failure = details.FailureConverter.ToFailure(
                        e, details.PayloadConverter);
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
                    // TODO(cretz): This is only here for now to stop analyzer check error saying we
                    // never assign to this property. Fix up when building cancel infra.
                    cancelRequested = true;
                    break;
                case WorkflowActivationJob.VariantOneofCase.RemoveFromCache:
                    // Ignore, handled outside the instance
                    break;
                case WorkflowActivationJob.VariantOneofCase.StartWorkflow:
                    // Don't need parameter, already in instance info
                    ApplyStartWorkflow();
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognized job: {job.VariantCase}");
            }
        }

        private void ApplyStartWorkflow()
        {
            QueueNewTask(() => RunTopLevelAsync(async () =>
            {
                var resultObj = await Inbound.ExecuteWorkflowAsync(new(
                    Instance: Instance,
                    RunMethod: details.Definition.RunMethod,
                    Parameters: StartParameters,
                    Headers: details.Start.Headers)).ConfigureAwait(false);
                var result = details.PayloadConverter.ToPayload(resultObj);
                AddCommand(new() { CompleteWorkflowExecution = new() { Result = result } });
            }));
        }

        /// <summary>
        /// Workflow inbound implementation.
        /// </summary>
        internal class InboundImpl : WorkflowInboundInterceptor
        {
            /// <summary>
            /// Gets the outbound implementation.
            /// </summary>
            internal WorkflowOutboundInterceptor? Outbound { get; private set; }

            /// <inheritdoc />
            public override void Init(WorkflowOutboundInterceptor outbound)
            {
                Outbound = outbound;
            }

            /// <inheritdoc />
            public override async Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
            {
                // Have to unwrap and re-throw target invocation exception if present
                Task resultTask;
                try
                {
                    resultTask = (Task)input.RunMethod.Invoke(input.Instance, input.Parameters)!;
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException!;
                }
                // Since the result is a task, we need to await on it and use that result
                await resultTask.ConfigureAwait(false);
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
        }
    }
}