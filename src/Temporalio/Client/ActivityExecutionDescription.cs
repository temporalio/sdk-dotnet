using System;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Client
{
    /// <summary>
    /// Description of a standalone activity execution from a describe call.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityExecutionDescription : ActivityExecution
    {
        private readonly Lazy<Task<(string? Summary, string? Details)>> userMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityExecutionDescription"/> class.
        /// </summary>
        /// <param name="rawDescription">Raw proto description.</param>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
        protected internal ActivityExecutionDescription(
            DescribeActivityExecutionResponse rawDescription,
            string clientNamespace,
            DataConverter dataConverter)
            : base(
                clientNamespace: clientNamespace,
                activityId: rawDescription.Info.ActivityId,
                activityRunId: string.IsNullOrEmpty(rawDescription.Info.RunId) ? null : rawDescription.Info.RunId,
                activityType: rawDescription.Info.ActivityType?.Name ?? string.Empty,
                closeTime: rawDescription.Info.CloseTime?.ToDateTime(),
                executionDuration: rawDescription.Info.ExecutionDuration?.ToTimeSpan(),
                scheduledTime: rawDescription.Info.ScheduleTime?.ToDateTime() ?? default,
                stateTransitionCount: rawDescription.Info.StateTransitionCount,
                status: rawDescription.Info.Status,
                taskQueue: rawDescription.Info.TaskQueue,
                searchAttributesFactory: () => rawDescription.Info.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(rawDescription.Info.SearchAttributes))
        {
            RawDescription = rawDescription;
            var info = rawDescription.Info;
            Attempt = info.Attempt;
            CanceledReason = string.IsNullOrEmpty(info.CanceledReason) ? null : info.CanceledReason;
            CurrentRetryInterval = info.CurrentRetryInterval?.ToTimeSpan();
            ExpirationTime = info.ExpirationTime?.ToDateTime();
            HeartbeatTimeout = info.HeartbeatTimeout?.ToTimeSpan();
            LastAttemptCompleteTime = info.LastAttemptCompleteTime?.ToDateTime();
            LastHeartbeatTime = info.LastHeartbeatTime?.ToDateTime();
            LastStartedTime = info.LastStartedTime?.ToDateTime();
            LastWorkerIdentity = string.IsNullOrEmpty(info.LastWorkerIdentity) ? null : info.LastWorkerIdentity;
            LongPollToken = rawDescription.LongPollToken.IsEmpty ? null : rawDescription.LongPollToken.ToByteArray();
            NextAttemptScheduleTime = info.NextAttemptScheduleTime?.ToDateTime();
            RetryPolicy = info.RetryPolicy == null ? null : Common.RetryPolicy.FromProto(info.RetryPolicy);
            RunState = info.RunState;
            ScheduleToCloseTimeout = info.ScheduleToCloseTimeout?.ToTimeSpan();
            ScheduleToStartTimeout = info.ScheduleToStartTimeout?.ToTimeSpan();
            StartToCloseTimeout = info.StartToCloseTimeout?.ToTimeSpan();
#pragma warning disable VSTHRD011 // This should not be able to deadlock
            userMetadata = new(() => dataConverter.FromUserMetadataAsync(info.UserMetadata));
#pragma warning restore VSTHRD011
        }

        /// <summary>
        /// Gets the current attempt number, starting at 1.
        /// </summary>
        public int Attempt { get; private init; }

        /// <summary>
        /// Gets the reason for cancellation, if cancel was requested.
        /// </summary>
        public string? CanceledReason { get; private init; }

        /// <summary>
        /// Gets the time until the next retry, if applicable.
        /// </summary>
        public TimeSpan? CurrentRetryInterval { get; private init; }

        /// <summary>
        /// Gets the scheduled time plus schedule-to-close timeout.
        /// </summary>
        public DateTime? ExpirationTime { get; private init; }

        /// <summary>
        /// Gets the heartbeat timeout.
        /// </summary>
        public TimeSpan? HeartbeatTimeout { get; private init; }

        /// <summary>
        /// Gets when the last attempt completed.
        /// </summary>
        public DateTime? LastAttemptCompleteTime { get; private init; }

        /// <summary>
        /// Gets the time of the last heartbeat.
        /// </summary>
        public DateTime? LastHeartbeatTime { get; private init; }

        /// <summary>
        /// Gets when the last attempt was started.
        /// </summary>
        public DateTime? LastStartedTime { get; private init; }

        /// <summary>
        /// Gets the identity of the last worker that processed the activity.
        /// </summary>
        public string? LastWorkerIdentity { get; private init; }

        /// <summary>
        /// Gets the token for follow-on long-poll requests, or null if the activity is complete.
        /// </summary>
        public byte[]? LongPollToken { get; private init; }

        /// <summary>
        /// Gets when the next attempt will be scheduled.
        /// </summary>
        public DateTime? NextAttemptScheduleTime { get; private init; }

        /// <summary>
        /// Gets the retry policy for the activity.
        /// </summary>
        public RetryPolicy? RetryPolicy { get; private init; }

        /// <summary>
        /// Gets the more detailed run state if the activity status is running.
        /// </summary>
        public PendingActivityState RunState { get; private init; }

        /// <summary>
        /// Gets the schedule-to-close timeout.
        /// </summary>
        public TimeSpan? ScheduleToCloseTimeout { get; private init; }

        /// <summary>
        /// Gets the schedule-to-start timeout.
        /// </summary>
        public TimeSpan? ScheduleToStartTimeout { get; private init; }

        /// <summary>
        /// Gets the start-to-close timeout.
        /// </summary>
        public TimeSpan? StartToCloseTimeout { get; private init; }

        /// <summary>
        /// Gets the raw proto description.
        /// </summary>
        internal DescribeActivityExecutionResponse RawDescription { get; private init; }

        /// <summary>
        /// Gets the single-line fixed summary for this activity execution that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        /// <returns>Static summary.</returns>
        public async Task<string?> GetStaticSummaryAsync() =>
            (await userMetadata.Value.ConfigureAwait(false)).Summary;

        /// <summary>
        /// Gets the general fixed details for this activity execution that may appear in UI/CLI.
        /// This can be in Temporal markdown format and can span multiple lines.
        /// </summary>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        /// <returns>Static details.</returns>
        public async Task<string?> GetStaticDetailsAsync() =>
            (await userMetadata.Value.ConfigureAwait(false)).Details;
    }
}
