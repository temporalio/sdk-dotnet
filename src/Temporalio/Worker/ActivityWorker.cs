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
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Common;
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
        private readonly ActivityDefinition? dynamicActivity;
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
                if (defn.Name == null)
                {
                    if (dynamicActivity != null)
                    {
                        throw new ArgumentException("Multiple dynamic activities provided");
                    }
                    dynamicActivity = defn;
                }
                else if (activities.ContainsKey(defn.Name))
                {
                    throw new ArgumentException($"Duplicate activity named {defn.Name}");
                }
                else
                {
                    activities[defn.Name] = defn;
                }
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ActivityWorker"/> class.
        /// </summary>
        ~ActivityWorker() => Dispose(false);

        /// <summary>
        /// Execute the activity worker until poller shutdown or failure. If there is a failure,
        /// this may need to be called a second time after shutdown initiated to ensure activity
        /// tasks are drained.
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
                            try
                            {
                                StartActivity(task);
                            }
                            catch (Exception e)
                            {
                                logger.LogError(e, "Failed starting activity task");
                                await worker.BridgeWorker.CompleteActivityTaskAsync(new()
                                {
                                    TaskToken = task.TaskToken,
                                    Result = new()
                                    {
                                        Failed = new()
                                        {
                                            Failure_ = new()
                                            {
                                                Message = $"Failed starting activity task: {e}",
                                                ApplicationFailureInfo = new() { Type = e.GetType().Name },
                                            },
                                        },
                                    },
                                }).ConfigureAwait(false);
                            }
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
                                act.Cancel(task.Cancel);
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
        /// Checks if the given activity type is registered on this worker.
        /// </summary>
        /// <param name="activityType">The type of activity to look up. </param>
        public void AssertValidActivity(string activityType)
        {
            if (dynamicActivity != null || activities.ContainsKey(activityType))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Activity {activityType} is not registered on this worker," +
                $" available activities: {ActivityList()}");
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

        private string ActivityList()
        {
            var avail = activities.Keys.ToList();
            avail.Sort();
            return string.Join(", ", avail);
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
            // Activity-specific data converter
            var start = tsk.Start;
            var dataConverter = worker.Client.Options.DataConverter.WithSerializationContext(
                new ISerializationContext.Activity(
                    Namespace: string.IsNullOrEmpty(start.WorkflowNamespace) ? worker.Client.Options.Namespace : start.WorkflowNamespace,
                    ActivityId: start.ActivityId,
                    WorkflowId: string.IsNullOrEmpty(start.WorkflowExecution?.WorkflowId) ? null : start.WorkflowExecution!.WorkflowId,
                    WorkflowType: string.IsNullOrEmpty(start.WorkflowType) ? null : start.WorkflowType,
                    ActivityType: start.ActivityType,
                    ActivityTaskQueue: worker.Options.TaskQueue!,
                    IsLocal: start.IsLocal));

            // Create info. Workflow fields are null for standalone activities.
            var workflowId = string.IsNullOrEmpty(start.WorkflowExecution?.WorkflowId) ? null : start.WorkflowExecution!.WorkflowId;
            var info = new ActivityInfo(
                ActivityId: start.ActivityId,
                ActivityType: start.ActivityType,
                Attempt: (int)start.Attempt,
                // TODO(cretz): CurrentAttemptScheduledTime is not set for standalone
                // activities. Use zero time as fallback until server populates this.
                CurrentAttemptScheduledTime: start.CurrentAttemptScheduledTime?.ToDateTime() ?? DateTime.MinValue,
                DataConverter: dataConverter,
                HeartbeatDetails: start.HeartbeatDetails,
                HeartbeatTimeout: OptionalTimeSpan(start.HeartbeatTimeout),
                IsLocal: start.IsLocal,
                Namespace: string.IsNullOrEmpty(start.WorkflowNamespace) ? worker.Client.Options.Namespace : start.WorkflowNamespace,
                Priority: start.Priority is { } p ? new(p) : Priority.Default,
                RetryPolicy: start.RetryPolicy is { } rp ? RetryPolicy.FromProto(rp) : null,
                ScheduleToCloseTimeout: OptionalTimeSpan(start.ScheduleToCloseTimeout),
                ScheduledTime: start.ScheduledTime.ToDateTime(),
                StartToCloseTimeout: OptionalTimeSpan(start.StartToCloseTimeout),
                StartedTime: start.StartedTime.ToDateTime(),
                TaskQueue: worker.Options.TaskQueue!,
                TaskToken: tsk.TaskToken.ToByteArray(),
                WorkflowId: workflowId,
                WorkflowNamespace: workflowId != null ? start.WorkflowNamespace : null,
                WorkflowRunId: workflowId != null ? start.WorkflowExecution!.RunId : null,
                WorkflowType: workflowId != null ? start.WorkflowType : null);
            // Create context
            var cancelTokenSource = new CancellationTokenSource();
            var context = new ActivityExecutionContext(
                info: info,
                cancellationToken: cancelTokenSource.Token,
                workerShutdownToken: workerShutdownTokenSource.Token,
                taskToken: tsk.TaskToken,
                logger: worker.LoggerFactory.CreateLogger($"Temporalio.Activity:{info.ActivityType}"),
                payloadConverter: dataConverter.PayloadConverter,
                runtimeMetricMeter: worker.MetricMeter,
                temporalClient: worker.Client as ITemporalClient);

            // Start task
            using (context.Logger.BeginScope(info.LoggerScope))
            {
                // We know that the task for the running activity is accessed only on graceful
                // shutdown which could never run until after this (and the primary execute) are
                // done. So we don't have to worry about the dictionary having an activity without
                // a task even though we're late-binding it here.
                var act = new RunningActivity(context, cancelTokenSource, dataConverter);
                runningActivities[tsk.TaskToken] = act;
#pragma warning disable CA2008 // We don't have to pass a scheduler, factory already implies one
                act.Task = worker.Options.ActivityTaskFactory.StartNew(
                    () => ExecuteActivityAsync(act, tsk, dataConverter)).Unwrap();
#pragma warning restore CA2008
            }
        }

        private async Task ExecuteActivityAsync(
            RunningActivity act,
            Bridge.Api.ActivityTask.ActivityTask tsk,
            DataConverter dataConverter)
        {
            // Try to build a completion, but if it fails then manually build one with the failure
            Bridge.Api.ActivityTaskCompletion completion;
            try
            {
                completion = await ExecuteActivityInternalAsync(
                    act, tsk, dataConverter).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                completion = new()
                {
                    TaskToken = tsk.TaskToken,
                    Result = new()
                    {
                        Failed = new()
                        {
                            Failure_ = new()
                            {
                                Message = $"Failed building completion: {e}",
                                ApplicationFailureInfo = new() { Type = e.GetType().Name },
                            },
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
            Bridge.Api.ActivityTask.ActivityTask tsk,
            DataConverter dataConverter)
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
                    defn = dynamicActivity;
                    if (defn == null)
                    {
                        throw new ApplicationFailureException(
                            $"Activity {act.Context.Info.ActivityType} is not registered on this worker," +
                            $" available activities: {ActivityList()}",
                            errorType: "NotFoundError");
                    }
                }

                object?[] paramVals;
                try
                {
                    // Dynamic activities are just one param of a raw array, otherwise, deserialize
                    if (defn.Dynamic)
                    {
                        paramVals = new[]
                        {
                            await Task.WhenAll(tsk.Start.Input.Select(p =>
                                dataConverter.ToValueAsync<IRawValue>(p))).
                                ConfigureAwait(false),
                        };
                    }
                    else
                    {
                        // Deserialize arguments. If the input is less than the required parameter
                        // count, we error.
                        if (tsk.Start.Input.Count < defn.RequiredParameterCount)
                        {
                            throw new ApplicationFailureException(
                                $"Activity {act.Context.Info.ActivityType} given {tsk.Start.Input.Count} parameter(s)," +
                                " but more than that are required by the signature");
                        }
                        // Zip the params and input and then decode each. It is intentional that we
                        // discard extra input arguments that the signature doesn't accept.
                        paramVals = await Task.WhenAll(
                            tsk.Start.Input.Zip(defn.ParameterTypes, (input, paramType) =>
                                dataConverter.ToValueAsync(input, paramType))).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    throw new ApplicationFailureException("Failed decoding parameters", e);
                }

                // If there is a payload codec, use it to decode the headers
                if (dataConverter.PayloadCodec is IPayloadCodec codec)
                {
                    foreach (var kvp in tsk.Start.HeaderFields)
                    {
                        tsk.Start.HeaderFields[kvp.Key] =
                            await codec.DecodeSingleAsync(kvp.Value).ConfigureAwait(false);
                    }
                }

                // Build the interceptor impls, chaining each interceptor in reverse
                var inbound = worker.Interceptors.Reverse().Aggregate(
                    (ActivityInboundInterceptor)new InboundImpl(),
                    (v, impl) => impl.InterceptActivity(v));
                // Initialize with outbound
                inbound.Init(new OutboundImpl(this, act.Context.TaskToken));

                // Execute and put result on completed
                var result = await inbound.ExecuteActivityAsync(new(
                    Activity: defn,
                    Args: paramVals,
                    Headers: tsk.Start.HeaderFields)).ConfigureAwait(false);

                completion.Result.Completed = new();
                // As a special case, ValueTuple is considered "void" which is null
                if (result?.GetType() == typeof(ValueTuple))
                {
                    result = null;
                }
                completion.Result.Completed.Result =
                    await dataConverter.ToPayloadAsync(result).ConfigureAwait(false);
            }
            catch (CompleteAsyncException)
            {
                act.Context.Logger.LogDebug(
                    "Completing activity {ActivityType} asynchronously",
                    act.Context.Info.ActivityType);
                completion.Result.WillCompleteAsync = new();
            }
            catch (OperationCanceledException e) when (
                act.ServerRequestedCancel && act.Context.CancellationDetails?.IsPaused == true)
            {
                act.Context.Logger.LogDebug(
                    "Completing activity {ActivityType} as failed due cancel exception caused by pause",
                    act.Context.Info.ActivityType);
                completion.Result.Failed = new()
                {
                    Failure_ = await dataConverter.ToFailureAsync(
                        new ApplicationFailureException(
                            "Activity paused", e, "ActivityPause")).ConfigureAwait(false),
                };
            }
            catch (OperationCanceledException e) when (
                act.ServerRequestedCancel && act.Context.CancellationDetails?.IsReset == true)
            {
                act.Context.Logger.LogDebug(
                    "Completing activity {ActivityType} as failed due cancel exception caused by reset",
                    act.Context.Info.ActivityType);
                completion.Result.Failed = new()
                {
                    Failure_ = await dataConverter.ToFailureAsync(
                        new ApplicationFailureException(
                            "Activity reset", e, "ActivityReset")).ConfigureAwait(false),
                };
            }
            catch (OperationCanceledException) when (act.ServerRequestedCancel)
            {
                act.Context.Logger.LogDebug(
                    "Completing activity {ActivityType} as cancelled, reason: ",
                    act.Context.Info.ActivityType);
                completion.Result.Cancelled = new()
                {
                    Failure = await dataConverter.ToFailureAsync(
                        new CanceledFailureException("Cancelled")).ConfigureAwait(false),
                };
            }
            catch (OperationCanceledException) when (act.HeartbeatFailureException != null)
            {
                act.Context.Logger.LogWarning(
                    "Completing activity {ActivityType} as failure during heartbeat",
                    act.Context.Info.ActivityType);
                completion.Result.Failed = new()
                {
                    Failure_ = await dataConverter.ToFailureAsync(
                        act.HeartbeatFailureException).ConfigureAwait(false),
                };
            }
            catch (Exception e)
            {
                // Downgrade log level to DEBUG for benign application errors.
                if ((e as ApplicationFailureException)?.Category == ApplicationErrorCategory.Benign)
                {
                    act.Context.Logger.LogDebug(
                        e,
                        "Completing activity {ActivityType} as failed (benign)",
                        act.Context.Info.ActivityType);
                }
                else
                {
                    act.Context.Logger.LogWarning(
                        e,
                        "Completing activity {ActivityType} as failed",
                        act.Context.Info.ActivityType);
                }

                completion.Result.Failed = new()
                {
                    Failure_ = await dataConverter.ToFailureAsync(e).ConfigureAwait(false),
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
            private readonly DataConverter dataConverter;

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
            /// <param name="dataConverter">Data converter.</param>
            public RunningActivity(
                ActivityExecutionContext context,
                CancellationTokenSource cancelTokenSource,
                DataConverter dataConverter)
            {
                Context = context;
                this.cancelTokenSource = cancelTokenSource;
                this.dataConverter = dataConverter;
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
            /// <param name="details">Cancellation details.</param>
            public void Cancel(ActivityCancelReason reason, ActivityCancellationDetails details)
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
                    Context.CancelRef.CancelReason = reason;
                    Context.CancelRef.CancellationDetails = details;
                    cancelTokenSource.Cancel();
                }
            }

            /// <summary>
            /// Cancel this activity with the given upstream info.
            /// </summary>
            /// <param name="cancel">Cancel.</param>
            public void Cancel(Bridge.Api.ActivityTask.Cancel cancel)
            {
                lock (mutex)
                {
                    serverRequestedCancel = true;
                }
                var details = new ActivityCancellationDetails()
                {
                    IsGoneFromServer = cancel.Details?.IsNotFound ?? false,
                    IsCancelRequested = cancel.Details?.IsCancelled ?? false,
                    IsTimedOut = cancel.Details?.IsTimedOut ?? false,
                    IsWorkerShutdown = cancel.Details?.IsWorkerShutdown ?? false,
                    IsPaused = cancel.Details?.IsPaused ?? false,
                    IsReset = cancel.Details?.IsReset ?? false,
                };
                switch (cancel.Reason)
                {
                    case Bridge.Api.ActivityTask.ActivityCancelReason.NotFound:
                        Cancel(ActivityCancelReason.GoneFromServer, details);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.Cancelled:
                        Cancel(ActivityCancelReason.CancelRequested, details);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.TimedOut:
                        Cancel(ActivityCancelReason.Timeout, details);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.WorkerShutdown:
                        Cancel(ActivityCancelReason.WorkerShutdown, details);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.Paused:
                        Cancel(ActivityCancelReason.Paused, details);
                        break;
                    case Bridge.Api.ActivityTask.ActivityCancelReason.Reset:
                        Cancel(ActivityCancelReason.Reset, details);
                        break;
                    default:
                        Cancel(ActivityCancelReason.None, details);
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
                                await dataConverter.ToPayloadsAsync(details).ConfigureAwait(false));
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
                        Cancel(
                            ActivityCancelReason.HeartbeatRecordFailure,
                            new() { IsHeartbeatRecordFailure = true });
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
            private readonly ByteString taskToken;

            /// <summary>
            /// Initializes a new instance of the <see cref="OutboundImpl"/> class.
            /// </summary>
            /// <param name="worker">Activity worker.</param>
            /// <param name="taskToken">Activity task token.</param>
            public OutboundImpl(ActivityWorker worker, ByteString taskToken)
            {
                this.worker = worker;
                this.taskToken = taskToken;
            }

            /// <inheritdoc />
            public override void Heartbeat(HeartbeatInput input)
            {
                if (worker.runningActivities.TryGetValue(taskToken, out var act))
                {
                    act.Heartbeat(worker.worker, input.Details);
                }
            }
        }
    }
}
