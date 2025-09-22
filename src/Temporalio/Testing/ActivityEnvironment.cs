using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Testing
{
    /// <summary>
    /// Testing environment which populates the <see cref="ActivityExecutionContext" /> for test
    /// code.
    /// </summary>
    public record ActivityEnvironment
    {
        /// <summary>
        /// Default activity info.
        /// </summary>
        public static readonly ActivityInfo DefaultInfo = new(
            ActivityId: "test",
            ActivityType: "unknown",
            Attempt: 1,
            CurrentAttemptScheduledTime: new(1970, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            DataConverter: DataConverter.Default,
            HeartbeatDetails: Array.Empty<Payload>(),
            HeartbeatTimeout: null,
            IsLocal: false,
            Priority: new(),
            RetryPolicy: new(),
            ScheduleToCloseTimeout: TimeSpan.FromSeconds(1),
            ScheduledTime: new(1970, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            StartToCloseTimeout: TimeSpan.FromSeconds(1),
            StartedTime: new(1970, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            TaskQueue: "test",
            TaskToken: System.Text.Encoding.ASCII.GetBytes("test"),
            WorkflowId: "test",
            WorkflowNamespace: "default",
            WorkflowRunId: "test-run",
            WorkflowType: "test");

        /// <summary>
        /// Gets or inits the activity info. Default is <see cref="DefaultInfo" />.
        /// </summary>
        public ActivityInfo Info { get; init; } = DefaultInfo;

        /// <summary>
        /// Gets or inits the logger. Default is a no-op logger.
        /// </summary>
        public ILogger Logger { get; init; } = NullLogger.Instance;

        /// <summary>
        /// Gets or inits the payload converter. Default is default converter.
        /// </summary>
        public IPayloadConverter PayloadConverter { get; init; } = DataConverter.Default.PayloadConverter;

        /// <summary>
        /// Gets or inits the metric meter for this activity. If unset, a noop meter is used.
        /// </summary>
        public MetricMeter? MetricMeter { get; init; }

        /// <summary>
        /// Gets or inits the Temporal client accessible from the activity context. If unset, an
        /// exception is thrown when the client is accessed.
        /// </summary>
        public ITemporalClient? TemporalClient { get; init; }

        /// <summary>
        /// Gets or sets the cancel reason. Callers should use one of the overloads of
        /// <see cref="Cancel()" /> instead.
        /// </summary>
        public ActivityCancelReason CancelReason
        {
            get => CancelRef.CancelReason;
            set => CancelRef.CancelReason = value;
        }

        /// <summary>
        /// Gets or sets the cancellation details. Callers should use one of the overloads of
        /// <see cref="Cancel()" /> instead.
        /// </summary>
        public ActivityCancellationDetails? CancellationDetails
        {
            get => CancelRef.CancellationDetails;
            set => CancelRef.CancellationDetails = value;
        }

        /// <summary>
        /// Gets or inits the cancellation token source.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; init; } = new();

        /// <summary>
        /// Gets or inits the worker shutdown token source.
        /// </summary>
        public CancellationTokenSource WorkerShutdownTokenSource { get; init; } = new();

        /// <summary>
        /// Gets or inits the heartbeat callback. Default is a no-op callback.
        /// </summary>
        public Action<object?[]> Heartbeater { get; init; } = details => { };

        /// <summary>
        /// Gets the cancel reference.
        /// </summary>
        internal ActivityCancelRef CancelRef { get; } = new();

        /// <summary>
        /// Run the given activity with a context.
        /// </summary>
        /// <param name="activity">Activity to run.</param>
        /// <returns>Task for activity completion.</returns>
        public Task RunAsync(Action activity) => RunAsync(() => new Task(activity));

        /// <summary>
        /// Run the given activity with a context.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="activity">Activity to run.</param>
        /// <returns>Activity result.</returns>
        public Task<T> RunAsync<T>(Func<T> activity) => RunAsync(() => Task.FromResult(activity()));

        /// <summary>
        /// Run the given activity with a context.
        /// </summary>
        /// <param name="activity">Activity to run.</param>
        /// <returns>Task for activity completion.</returns>
        public Task RunAsync(Func<Task> activity) =>
            RunAsync<object?>(async () =>
            {
                await activity().ConfigureAwait(false);
                return null;
            });

        /// <summary>
        /// Run the given activity with a context.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="activity">Activity to run.</param>
        /// <returns>Activity result.</returns>
        public async Task<T> RunAsync<T>(Func<Task<T>> activity)
        {
            try
            {
                ActivityExecutionContext.AsyncLocalCurrent.Value = new(
                    info: Info,
                    cancellationToken: CancellationTokenSource.Token,
                    workerShutdownToken: WorkerShutdownTokenSource.Token,
                    // This task token is not used for anything in this env
                    taskToken: ByteString.Empty,
                    logger: Logger,
                    payloadConverter: PayloadConverter,
                    runtimeMetricMeter: new(() => MetricMeter ?? MetricMeterNoop.Instance),
                    temporalClient: TemporalClient)
                {
                    Heartbeater = Heartbeater,
                    CancelRef = CancelRef,
                };
                return await activity().ConfigureAwait(false);
            }
            finally
            {
                ActivityExecutionContext.AsyncLocalCurrent.Value = null;
            }
        }

        /// <summary>
        /// If cancellation not already requested, set the cancel reason/details and cancel the
        /// token source.
        /// </summary>
        public void Cancel() =>
            Cancel(ActivityCancelReason.CancelRequested);

        /// <summary>
        /// If cancellation not already requested, set the cancel reason/details and cancel the
        /// token source.
        /// </summary>
        /// <param name="reason">Cancel reason.</param>
        public void Cancel(ActivityCancelReason reason) =>
            Cancel(reason, new() { IsCancelRequested = true });

        /// <summary>
        /// If cancellation not already requested, set the cancel reason/details and cancel the
        /// token source.
        /// </summary>
        /// <param name="reason">Cancel reason.</param>
        /// <param name="details">Cancellation details.</param>
        public void Cancel(ActivityCancelReason reason, ActivityCancellationDetails details)
        {
            // This is intentionally not an atomic operation same as it's not in the real worker.
            // It is documented for callers not to expect reason to be valid until cancellation
            // token is set.
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                CancelReason = reason;
                CancellationDetails = details;
                CancellationTokenSource.Cancel();
            }
        }
    }
}
