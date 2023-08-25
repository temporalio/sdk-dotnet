using System;
using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Activities
{
    /// <summary>
    /// Context that is available during activity executions. Use <see cref="Current" /> to get the
    /// context. Contexts are <see cref="AsyncLocal{T}" /> to activities. <see cref="HasCurrent" />
    /// can be used to check whether a context is available.
    /// </summary>
    public class ActivityExecutionContext
    {
        private readonly Lazy<IMetricMeter> metricMeter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityExecutionContext"/> class.
        /// </summary>
        /// <param name="info">Activity info.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="workerShutdownToken">Workflow shutdown token.</param>
        /// <param name="taskToken">Raw activity task token.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="payloadConverter">Payload converter.</param>
        /// <param name="runtimeMetricMeter">Runtime-level metric meter.</param>
#pragma warning disable CA1068 // We don't require cancellation token as last param
        internal ActivityExecutionContext(
            ActivityInfo info,
            CancellationToken cancellationToken,
            CancellationToken workerShutdownToken,
            ByteString taskToken,
            ILogger logger,
            IPayloadConverter payloadConverter,
            Lazy<IMetricMeter> runtimeMetricMeter)
        {
            Info = info;
            CancellationToken = cancellationToken;
            WorkerShutdownToken = workerShutdownToken;
            TaskToken = taskToken;
            Logger = logger;
            PayloadConverter = payloadConverter;
            metricMeter = new(() =>
            {
                return runtimeMetricMeter.Value.WithTags(new Dictionary<string, object>()
                {
                    { "namespace", info.WorkflowNamespace },
                    { "task_queue", info.TaskQueue },
                    { "activity_type", info.ActivityType },
                });
            });
        }
#pragma warning restore CA1068

        /// <summary>
        /// Gets a value indicating whether the current code is running in an activity.
        /// </summary>
        public static bool HasCurrent => AsyncLocalCurrent.Value != null;

        /// <summary>
        /// Gets the current activity context.
        /// </summary>
        /// <exception cref="InvalidOperationException">If no context is available.</exception>
        public static ActivityExecutionContext Current => AsyncLocalCurrent.Value ??
            throw new InvalidOperationException("No current context");

        /// <summary>
        /// Gets the info for this activity.
        /// </summary>
        public ActivityInfo Info { get; private init; }

        /// <summary>
        /// Gets the logger scoped to this activity.
        /// </summary>
        public ILogger Logger { get; private init; }

        /// <summary>
        /// Gets why the activity was cancelled. This value is inaccurate until
        /// <see cref="CancellationToken" /> is cancelled.
        /// </summary>
        public ActivityCancelReason CancelReason => CancelReasonRef.CancelReason;

        /// <summary>
        /// Gets the cancellation token that is cancelled when the activity is cancelled.
        /// </summary>
        public CancellationToken CancellationToken { get; private init; }

        /// <summary>
        /// Gets the cancellation token that is cancelled when the worker is shutdown. This can be
        /// used to gracefully handle worker shutdown for
        /// <see cref="Worker.TemporalWorkerOptions.GracefulShutdownTimeout" /> before
        /// <see cref="CancellationToken" /> will ultimately be cancelled.
        /// </summary>
        public CancellationToken WorkerShutdownToken { get; private init; }

        /// <summary>
        /// Gets the payload converter in use by this activity worker.
        /// </summary>
        public IPayloadConverter PayloadConverter { get; private init; }

        /// <summary>
        /// Gets the metric meter for this activity with activity-specific tags. Note, this is
        /// lazily created for each activity execution.
        /// </summary>
        public IMetricMeter MetricMeter => metricMeter.Value;

        /// <summary>
        /// Gets the async local current value.
        /// </summary>
        internal static AsyncLocal<ActivityExecutionContext?> AsyncLocalCurrent { get; } = new();

        /// <summary>
        /// Gets or sets the heartbeater. This is late-bound since interceptors that need this
        /// context may be invoked before the interceptor is created that ends up providing the
        /// heartbeater.
        /// </summary>
        internal Action<object?[]>? Heartbeater { get; set; }

        /// <summary>
        /// Gets a reference to the reason enum.
        /// </summary>
        internal ActivityCancelReasonRef CancelReasonRef { get; init; } = new();

        /// <summary>
        /// Gets the raw proto task token for this activity.
        /// </summary>
        internal ByteString TaskToken { get; private init; }

        /// <summary>
        /// Record a heartbeat on the activity.
        /// </summary>
        /// <remarks>
        /// Heartbeats should be used for all non-immediately-returning, non-local activities and
        /// they are required to receive cancellation. Heartbeats are queued and processed
        /// asynchronously, so this will not error if the details cannot be converted. Rather any
        /// error converting heartbeat details will result in activity cancellation then activity
        /// failure.
        /// <para>
        /// Heartbeat calls are throttled internally based on the heartbeat timeout of the activity.
        /// Users do not have to be concerned with burdening the server by calling this too
        /// frequently.
        /// </para>
        /// </remarks>
        /// <param name="details">Details to record with the heartbeat if any.</param>
        public void Heartbeat(params object?[] details)
        {
            if (Heartbeater == null)
            {
                throw new InvalidOperationException("Heartbeater not set yet");
            }
            Heartbeater.Invoke(details);
        }
    }
}