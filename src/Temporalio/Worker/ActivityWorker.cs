#pragma warning disable CA1031 // We do want to catch _all_ exceptions in this file sometimes

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker.Interceptors;

namespace Temporalio.Worker
{
    /// <summary>
    /// Worker for activities.
    /// </summary>
    internal class ActivityWorker : IDisposable
    {
        private readonly TemporalWorker worker;
        private readonly ILogger logger;
        // Keyed by name
        private readonly Dictionary<string, ActivityDefinition> activities;
        // Keyed by task token
        private readonly ConcurrentDictionary<ByteString, RunningActivity> runningActivities = new();

        private readonly CancellationTokenSource workerShutdownTokenSource = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityWorker"/> class.
        /// </summary>
        /// <param name="worker">Parent worker.</param>
        public ActivityWorker(TemporalWorker worker)
        {
            this.worker = worker;
            logger = worker.LoggerFactory.CreateLogger<ActivityWorker>();
            activities = new(worker.Options.Activities.Count);
            foreach (var defn in worker.Options.Activities)
            {
                if (activities.ContainsKey(defn.Name))
                {
                    throw new ArgumentException($"Duplicate activity named {defn.Name}");
                }
                activities[defn.Name] = defn;
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ActivityWorker"/> class.
        /// </summary>
        ~ActivityWorker() => Dispose(false);

        /// <summary>
        /// Execute the activity until poller shutdown or failure. If there is a failure, this may
        /// need to be called a second time after shutdown initiated to ensure activity tasks are
        /// drained.
        /// </summary>
        /// <returns>Task that only completes successfully on poller shutdown.</returns>
        public async Task ExecuteAsync()
        {
            // Run poll loop until there is no poll left
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                ["TaskQueue"] = worker.Options.TaskQueue!,
            }))
            {
                while (true)
                {
                    var task = await worker.BridgeWorker.PollActivityTaskAsync().ConfigureAwait(false);
                    logger.LogTrace("Received activity task: {Task}", task);
                    switch (task?.VariantCase)
                    {
                        case Bridge.Api.ActivityTask.ActivityTask.VariantOneofCase.Start:
                            StartActivity(task);
                            break;
                        case Bridge.Api.ActivityTask.ActivityTask.VariantOneofCase.Cancel:
                            if (!runningActivities.TryGetValue(task.TaskToken, out var act))
                            {
                                logger.LogWarning(
                                    "Cannot find activity to cancel for token {TaskToken}",
                                    task.TaskToken);
                            }
                            else
                            {
                                act.Cancel(task.Cancel.Reason);
                            }
                            break;
                        case null:
                            // This means worker shut down
                            return;
                        default:
                            throw new InvalidOperationException(
                                $"Unexpected activity task case {task?.VariantCase}");
                    }
                }
            }
        }

        /// <summary>
        /// Notify all running activities a worker shutdown is happening.
        /// </summary>
        public void NotifyShutdown()
        {
            using (logger.BeginScope(new Dictionary<string, object>()
            {
                ["TaskQueue"] = worker.Options.TaskQueue!,
            }))
            {
                logger.LogInformation(
                    "Beginning activity worker shutdown, will wait {GracefulShutdownTimeout} before " +
                    "cancelling {ActivityCount} activity instance(s)",
                    worker.Options.GracefulShutdownTimeout,
                    runningActivities.Count);
                workerShutdownTokenSource.Cancel();
            }
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
                workerShutdownTokenSource.Dispose();
            }
        }

        private void StartActivity(Bridge.Api.ActivityTask.ActivityTask tsk)
        {
            static TimeSpan? OptionalTimeSpan(Duration? dur)
            {
                if (dur == null)
                {
                    return null;
                }
                var span = dur.ToTimeSpan();
                return span == TimeSpan.Zero ? null : span;
            }
            // Create info
            var start = tsk.Start;
            var info = new ActivityInfo(
                ActivityID: start.ActivityId,
                ActivityType: start.ActivityType,
                Attempt: (int)start.Attempt,
                CurrentAttemptScheduledTime: start.CurrentAttemptScheduledTime.ToDateTime(),
                DataConverter: worker.Client.Options.DataConverter,
                HeartbeatDetails: start.HeartbeatDetails,
                HeartbeatTimeout: OptionalTimeSpan(start.HeartbeatTimeout),
                IsLocal: start.IsLocal,
                ScheduleToCloseTimeout: OptionalTimeSpan(start.ScheduleToCloseTimeout),
                ScheduledTime: start.ScheduledTime.ToDateTime(),
                StartToCloseTimeout: OptionalTimeSpan(start.StartToCloseTimeout),
                StartedTime: start.StartedTime.ToDateTime(),
                TaskQueue: worker.Options.TaskQueue!,
                TaskToken: tsk.TaskToken.ToByteArray(),
                WorkflowID: start.WorkflowExecution.WorkflowId,
                WorkflowNamespace: start.WorkflowNamespace,
                WorkflowRunID: start.WorkflowExecution.RunId,
                WorkflowType: start.WorkflowType);
            // Create context
            var cancelTokenSource = new CancellationTokenSource();
            var context = new ActivityExecutionContext(
                info: info,
                cancellationToken: cancelTokenSource.Token,
                workerShutdownToken: workerShutdownTokenSource.Token,
                tsk.TaskToken,
                worker.LoggerFactory.CreateLogger($"Temporalio.Activity:{info.ActivityType}"));

            // Start task
            using (context.Logger.BeginScope(info.LoggerScope))
            {
                // We know that the task for the running activity is accessed only on graceful
                // shutdown which could never run until after this (and the primary execute) are
                // done. So we don't have to worry about the dictionary having an activity without
                // a task even though we're late-binding it here.
                var act = new RunningActivity(context, cancelTokenSource);
                runningActivities[tsk.TaskToken] = act;
#pragma warning disable CA2008 // We don't have to pass a scheduler, factory already implies one
                act.Task = worker.Options.ActivityTaskFactory.StartNew(
                    () => ExecuteActivityAsync(act, tsk)).Unwrap();
#pragma warning restore CA2008
            }
        }

        private async Task ExecuteActivityAsync(
            RunningActivity act,
            Bridge.Api.ActivityTask.ActivityTask tsk)
        {
            // Try to build a completion, but if it fails then manually build one with the failure
            Bridge.Api.ActivityTaskCompletion completion;
            try
            {
                completion = await ExecuteActivityInternalAsync(act, tsk).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                completion = new()
                {
                    Result = new()
                    {
                        Failed = new()
                        {
                            Failure_ = new() { Message = $"Failed building completion: {e}" },
                        },
                    },
                };
            }

            // Try to finish heartbeats and send completion
            try
            {
                act.Context.Logger.LogTrace("Sending activity completion: {Completion}", completion);

                // We have to wait on any outstanding heartbeats to finish even after activity has
                // completed. FinishHeartbeatsAsync will not throw. We accept that in a rare
                // scenario, this heartbeat can fail to encode but it is too late to cancel/fail the
                // activity. Like other SDKs, we currently drop this heartbeat.
                //
                // If the completion is a success, we can finish heartbeat afterwards. Otherwise we
                // must finish before.
                if (completion.Result.Completed != null)
                {
                    await worker.BridgeWorker.CompleteActivityTaskAsync(
                        completion).ConfigureAwait(false);
                    await act.FinishHeartbeatsAsync().ConfigureAwait(false);
                }
                else
                {
                    await act.FinishHeartbeatsAsync().ConfigureAwait(false);
                    await worker.BridgeWorker.CompleteActivityTaskAsync(
                        completion).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                act.Context.Logger.LogError(
                    e,
                    "Failed completing activity task with completion {Completion}",
                    completion);
            }
            finally
            {
                // Remove from running activities
                runningActivities.TryRemove(tsk.TaskToken, out _);
            }
        }

        private async Task<Bridge.Api.ActivityTaskCompletion> ExecuteActivityInternalAsync(
            RunningActivity act,
            Bridge.Api.ActivityTask.ActivityTask tsk)
        {
            act.Context.Logger.LogDebug(
                "Running activity {ActivityType} (Token {TaskToken})",
                act.Context.Info.ActivityType,
                act.Context.TaskToken);
            // Completion to be sent back at end of activity
            var completion = new Bridge.Api.ActivityTaskCompletion()
            {
                TaskToken = tsk.TaskToken,
                Result = new(),
            };
            // Set context
            ActivityExecutionContext.AsyncLocalCurrent.Value = act.Context;
            try
            {
                // Find activity or fail
                if (!activities.TryGetValue(act.Context.Info.ActivityType, out var defn))
                {
                    var avail = activities.Keys.ToList();
                    avail.Sort();
                    var availStr = string.Join(", ", avail);
                    throw new ApplicationFailureException(
                        $"Activity {act.Context.Info.ActivityType} is not registered on this worker," +
                        $" available activities: {availStr}",
                        errorType: "NotFoundError");
                }

                // Deserialize arguments. If the input is less than the required parameter count, we
                // error.
                if (tsk.Start.Input.Count < defn.RequiredParameterCount)
                {
                    throw new ApplicationFailureException(
                        $"Activity {act.Context.Info.ActivityType} given {tsk.Start.Input.Count} parameter(s)," +
                        " but more than that are required by the signature");
                }
                // Zip the params and input and then decode each. It is intentional that we discard
                // extra input arguments that the signature doesn't accept.
                object?[] paramVals;
                try
                {
                    paramVals = await Task.WhenAll(
                        tsk.Start.Input.Zip(defn.ParameterTypes, (input, paramType) =>
                            worker.Client.Options.DataConverter.ToValueAsync(
                                input, paramType))).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    throw new ApplicationFailureException("Failed decoding parameters", e);
                }

                // Build the interceptor impls, chaining each interceptor in reverse
                var inbound = worker.Interceptors.Reverse().Aggregate(
                    (ActivityInboundInterceptor)new InboundImpl(),
                    (v, impl) => impl.InterceptActivity(v));
                // Initialize with outbound
                inbound.Init(new OutboundImpl(this));

                // Execute and put result on completed
                var result = await inbound.ExecuteActivityAsync(new(
                    Activity: defn,
                    Args: paramVals,
                    Headers: tsk.Start.HeaderFields)).ConfigureAwait(false);

                completion.Result.Completed = new();
                // As a special case, ValueTuple is considered "void"
                if (result?.GetType() != typeof(ValueTuple))
                {
                    completion.Result.Completed.Result =
                        await worker.Client.Options.DataConverter.ToPayloadAsync(
                            result).ConfigureAwait(false);
                }
            }
            catch (CompleteAsyncException)
            {
                act.Context.Logger.LogDebug(
                    "Completing activity {ActivityType} asynchronously",
                    act.Context.Info.ActivityType);
                completion.Result.WillCompleteAsync = new();
            }
            catch (OperationCanceledException) when (act.ServerRequestedCancel)
            {
                act.Context.Logger.LogDebug(
                    "Completing activity {ActivityType} as cancelled",
                    act.Context.Info.ActivityType);
                completion.Result.Cancelled = new()
                {
                    Failure = await worker.Client.Options.DataConverter.ToFailureAsync(
                        new CancelledFailureException("Cancelled")).ConfigureAwait(false),
                };
            }
            catch (OperationCanceledException) when (act.HeartbeatFailureException != null)
            {
                act.Context.Logger.LogWarning(
                    "Completing activity {ActivityType} as failure during heartbeat",
                    act.Context.Info.ActivityType);
                completion.Result.Failed = new()
                {
                    Failure_ = await worker.Client.Options.DataConverter.ToFailureAsync(
                        act.HeartbeatFailureException).ConfigureAwait(false),
                };
            }
            catch (Exception e)
            {
                act.Context.Logger.LogWarning(
                    e,
                    "Completing activity {ActivityType} as failed",
                    act.Context.Info.ActivityType);
                completion.Result.Failed = new()
                {
                    Failure_ = await worker.Client.Options.DataConverter.ToFailureAsync(
                        e).ConfigureAwait(false),
                };
            }
            finally
            {
                act.MarkDone();
                // Unset context just in case
                ActivityExecutionContext.AsyncLocalCurrent.Value = null;
            }
            return completion;
        }

        /// <summary>
        /// Representation of a running activity.
        /// </summary>
        internal class RunningActivity
        {
            private readonly CancellationTokenSource cancelTokenSource;

            private readonly object mutex = new();
            // All of these fields are locked on "mutex". While volatile could be used for the first
            // two since we don't have strict low-latency ordering guarantees, the lock does not
            // impose an unreasonable penalty.
            private bool serverRequestedCancel;
            private Exception? heartbeatFailureException;
            private object?[]? pendingHeartbeat;
            private object?[]? currentHeartbeat;
            private Task lastHeartbeatTask = Task.CompletedTask;
            private bool done;

            /// <summary>
            /// Initializes a new instance of the <see cref="RunningActivity"/> class.
            /// </summary>
            /// <param name="context">Activity context.</param>
            /// <param name="cancelTokenSource">Cancel source.</param>
            public RunningActivity(
                ActivityExecutionContext context, CancellationTokenSource cancelTokenSource)
            {
                Context = context;
                this.cancelTokenSource = cancelTokenSource;
            }

            /// <summary>
            /// Gets the activity context for this activity.
            /// </summary>
            public ActivityExecutionContext Context { get; private init; }

            /// <summary>
            /// Gets or sets the task for this activity.
            /// </summary>
            /// <remarks>
            /// This is late-bound because of how it's used. This is not thread safe.
            /// </remarks>
            public Task? Task { get; set; }

            /// <summary>
            /// Gets a value indicating whether the server has requested cancellation.
            /// </summary>
            public bool ServerRequestedCancel
            {
                get
                {
                    lock (mutex)
                    {
                        return serverRequestedCancel;
                    }
                }
            }

            /// <summary>
            /// Gets the heartbeat failure that caused cancellation.
            /// </summary>
            public Exception? HeartbeatFailureException
            {
                get
                {
                    lock (mutex)
                    {
                        return heartbeatFailureException;
                    }
                }
            }

            /// <summary>
            /// Mark this activity as done (mostly to ignore future heartbeats).
            /// </summary>
            public void MarkDone()
            {
                lock (mutex)
                {
                    done = true;
                }
                // We also will cancel the token just in case someone is using it asynchronously
                cancelTokenSource.Cancel();
            }

            /// <summary>
            /// Cancel this activity for the given reason if not already cancelled.
            /// </summary>
            /// <param name="reason">Cancel reason.</param>
            public void Cancel(ActivityCancelReason reason)
            {
                // Ignore if already cancelled
                if (cancelTokenSource.IsCancellationRequested)
                {
                    return;
                }
                using (Context.Logger.BeginScope(Context.Info.LoggerScope))
                {
                    Context.Logger.LogDebug(
                        "Cancelling activity {TaskToken}, reason {Reason}",
                        Context.TaskToken,
                        reason);
                    Context.CancelReasonRef.CancelReason = reason;
                    cancelTokenSource.Cancel();
                }
            }

            /// <summary>
            /// Cancel this activity for the given upstream reason.
            /// </summary>
            /// <param name="reason">Cancel reason.</param>
            public void Cancel(Bridge.Api.ActivityTask.ActivityCancelReason reason)
            {
                lock (mutex)
                {
                    serverRequestedCancel = true;
                }
                switch (reason)
                {
                    case Bridge.Api.ActivityTask.ActivityCancelReason.NotFound:
                        Cancel(ActivityCancelReason.GoneFromServer);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.Cancelled:
                        Cancel(ActivityCancelReason.CancelRequested);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.TimedOut:
                        Cancel(ActivityCancelReason.Timeout);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.WorkerShutdown:
                        Cancel(ActivityCancelReason.WorkerShutdown);
                        break;
                    default:
                        Cancel(ActivityCancelReason.None);
                        break;
                }
            }

            /// <summary>
            /// After marked done, wait for async heartbeats to complete. This will not throw.
            /// </summary>
            /// <returns>Task completed when heartbeats are done.</returns>
            public async Task FinishHeartbeatsAsync()
            {
                Task task;
                lock (mutex)
                {
                    task = lastHeartbeatTask;
                }
                await task.ConfigureAwait(false);
            }

            /// <summary>
            /// Asynchronously start a heartbeat.
            /// </summary>
            /// <param name="worker">Parent worker.</param>
            /// <param name="details">Heartbeat details.</param>
            public void Heartbeat(TemporalWorker worker, object?[] details)
            {
                // This needs to be atomic
                lock (mutex)
                {
                    // If done, do nothing
                    if (done)
                    {
                        return;
                    }
                    // If there is a current heartbeat, just set this as pending and the current
                    // task will pick it up. Otherwise, append a new task to process it.
                    if (currentHeartbeat != null)
                    {
                        pendingHeartbeat = details;
                    }
                    else
                    {
                        currentHeartbeat = details;
                        lastHeartbeatTask = lastHeartbeatTask.ContinueWith(
                            _ => HeartbeatAsync(worker),
                            worker.Options.ActivityTaskFactory.Scheduler ?? TaskScheduler.Current);
                    }
                }
            }

            private async Task HeartbeatAsync(TemporalWorker worker)
            {
                try
                {
                    // Heartbeat with the current details until there aren't any
                    while (true)
                    {
                        object?[]? details;
                        lock (mutex)
                        {
                            details = currentHeartbeat;
                            if (details == null)
                            {
                                return;
                            }
                            currentHeartbeat = pendingHeartbeat;
                            pendingHeartbeat = null;
                        }
                        var heartbeat = new Bridge.Api.ActivityHeartbeat()
                        {
                            TaskToken = Context.TaskToken,
                        };
                        if (details.Length > 0)
                        {
                            heartbeat.Details.AddRange(
                                await worker.Client.Options.DataConverter.ToPayloadsAsync(
                                    details).ConfigureAwait(false));
                        }
                        worker.BridgeWorker.RecordActivityHeartbeat(heartbeat);
                    }
                }
                catch (Exception e)
                {
                    using (Context.Logger.BeginScope(Context.Info.LoggerScope))
                    {
                        // Log exception on done (nowhere to propagate), warning + cancel if not
                        // done
                        bool done;
                        lock (mutex)
                        {
                            done = this.done;
                            heartbeatFailureException = e;
                        }
                        if (done)
                        {
                            Context.Logger.LogError(
                                e, "Failed recording heartbeat (activity already done, cannot error)");
                        }
                        else
                        {
                            Context.Logger.LogWarning(
                                e, "Cancelling activity because failed recording heartbeat");
                        }
                        Cancel(ActivityCancelReason.HeartbeatRecordFailure);
                    }
                }
            }
        }

        /// <summary>
        /// Inbound implementation.
        /// </summary>
        internal class InboundImpl : ActivityInboundInterceptor
        {
            /// <inheritdoc />
            public override void Init(ActivityOutboundInterceptor outbound)
            {
                // Set the context heartbeater as the outbound heartbeat if we're not local,
                // otherwise no-op
                if (ActivityExecutionContext.Current.Info.IsLocal)
                {
                    ActivityExecutionContext.Current.Heartbeater = details => { };
                }
                else
                {
                    ActivityExecutionContext.Current.Heartbeater =
                        details => outbound.Heartbeat(new(Details: details));
                }
            }

            /// <inheritdoc />
            public override Task<object?> ExecuteActivityAsync(ExecuteActivityInput input) =>
                input.Activity.InvokeAsync(input.Args);
        }

        /// <summary>
        /// Outbound implementation.
        /// </summary>
        internal class OutboundImpl : ActivityOutboundInterceptor
        {
            private readonly ActivityWorker worker;

            /// <summary>
            /// Initializes a new instance of the <see cref="OutboundImpl"/> class.
            /// </summary>
            /// <param name="worker">Activity worker.</param>
            public OutboundImpl(ActivityWorker worker) => this.worker = worker;

            /// <inheritdoc />
            public override void Heartbeat(HeartbeatInput input)
            {
                if (worker.runningActivities.TryGetValue(
                    ActivityExecutionContext.Current.TaskToken, out var act))
                {
                    act.Heartbeat(worker.worker, input.Details);
                }
            }
        }
    }
}