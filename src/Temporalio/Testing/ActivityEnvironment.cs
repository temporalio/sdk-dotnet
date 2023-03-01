using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Testing
{
    /// <summary>
    /// Testing environment which populates the <see cref="ActivityContext" /> for test code.
    /// </summary>
    public record ActivityEnvironment
    {
        /// <summary>
        /// Default activity info.
        /// </summary>
        public static readonly ActivityInfo DefaultInfo = new(
            ActivityID: "test",
            ActivityType: "unknown",
            Attempt: 1,
            CurrentAttemptScheduledTime: new(1970, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            DataConverter: DataConverter.Default,
            HeartbeatDetails: Array.Empty<Payload>(),
            HeartbeatTimeout: null,
            IsLocal: false,
            ScheduleToCloseTimeout: TimeSpan.FromSeconds(1),
            ScheduledTime: new(1970, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            StartToCloseTimeout: TimeSpan.FromSeconds(1),
            StartedTime: new(1970, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            TaskQueue: "test",
            TaskToken: System.Text.Encoding.ASCII.GetBytes("test"),
            WorkflowID: "test",
            WorkflowNamespace: "default",
            WorkflowRunID: "test-run",
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
        /// Gets or sets the cancel reason. Callers may prefer <see cref="Cancel" /> instead.
        /// </summary>
        public ActivityCancelReason CancelReason
        {
            get => CancelReasonRef.CancelReason;
            set => CancelReasonRef.CancelReason = value;
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
        /// Gets the cancel reason reference.
        /// </summary>
        internal ActivityCancelReasonRef CancelReasonRef { get; } = new();

        /// <summary>
        /// Run the given activity with a context.
        /// </summary>
        /// <param name="activity">Activity to run.</param>
        /// <returns>Task for activity completion.</returns>
        public Task RunAsync(Action activity)
        {
            return RunAsync(() => new Task(activity));
        }

        /// <summary>
        /// Run the given activity with a context.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="activity">Activity to run.</param>
        /// <returns>Activity result.</returns>
        public Task<T> RunAsync<T>(Func<T> activity)
        {
            return RunAsync(() => Task.FromResult(activity()));
        }

        /// <summary>
        /// Run the given activity with a context.
        /// </summary>
        /// <param name="activity">Activity to run.</param>
        /// <returns>Task for activity completion.</returns>
        public Task RunAsync(Func<Task> activity)
        {
            return RunAsync<object?>(async () =>
            {
                await activity().ConfigureAwait(false);
                return null;
            });
        }

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
                ActivityContext.AsyncLocalCurrent.Value = new(
                    info: Info,
                    cancellationToken: CancellationTokenSource.Token,
                    workerShutdownToken: WorkerShutdownTokenSource.Token,
                    // This task token is not used for anything in this env
                    taskToken: ByteString.Empty,
                    logger: Logger)
                {
                    Heartbeater = Heartbeater,
                    CancelReasonRef = CancelReasonRef,
                };
                return await activity().ConfigureAwait(false);
            }
            finally
            {
                ActivityContext.AsyncLocalCurrent.Value = null;
            }
        }

        /// <summary>
        /// If cancellation not already requested, set the cancel reason and cancel the token
        /// source.
        /// </summary>
        /// <param name="reason">Cancel reason.</param>
        public void Cancel(ActivityCancelReason reason = ActivityCancelReason.CancelRequested)
        {
            // This is intentionally not an atomic operation same as it's not in the real worker.
            // It is documented for callers not to expect reason to be valid until cancellation
            // token is set.
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                CancelReason = reason;
                CancellationTokenSource.Cancel();
            }
        }
    }
}