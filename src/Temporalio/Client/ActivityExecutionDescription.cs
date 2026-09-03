using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Api.Activity.V1;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Common;
using Temporalio.Converters;
using Priority = Temporalio.Common.Priority;
using RetryPolicy = Temporalio.Common.RetryPolicy;

namespace Temporalio.Client
{
    /// <summary>
    /// Description of a standalone activity execution from a describe call.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityExecutionDescription : ActivityExecution
    {
        private readonly ActivityDescribeOptions requestOptions;
        private readonly DataConverter dataConverter;
        private readonly Lazy<Task<(string? Summary, string? Details)>> userMetadata;
        private readonly Lazy<Task<Exception?>> lastFailure;
        private readonly Lazy<Task<Exception?>> outcomeFailure;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityExecutionDescription"/> class.
        /// </summary>
        /// <param name="requestOptions">Options used to make the request.</param>
        /// <param name="resp">Raw proto response.</param>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
        protected internal ActivityExecutionDescription(
            ActivityDescribeOptions? requestOptions,
            DescribeActivityExecutionResponse resp,
            string clientNamespace,
            DataConverter dataConverter)
            : base(
                clientNamespace: clientNamespace,
                activityId: resp.Info.ActivityId,
                activityRunId: string.IsNullOrEmpty(resp.Info.RunId) ? null : resp.Info.RunId,
                activityType: resp.Info.ActivityType?.Name ?? string.Empty,
                closeTime: resp.Info.CloseTime?.ToDateTime(),
                executionDuration: resp.Info.ExecutionDuration?.ToTimeSpan(),
                executionTime: resp.Info.ExecutionTime?.ToDateTime(),
                scheduleTime: resp.Info.ScheduleTime?.ToDateTime() ?? default,
                status: resp.Info.Status,
                taskQueue: resp.Info.TaskQueue,
                searchAttributesFactory: () => resp.Info.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(resp.Info.SearchAttributes))
        {
            this.requestOptions = requestOptions ?? new();
            this.dataConverter = dataConverter;
            RawResponse = resp;
            var info = resp.Info;
            Attempt = info.Attempt;
            CanceledReason = string.IsNullOrEmpty(info.CanceledReason) ? null : info.CanceledReason;
            CurrentRetryInterval = info.CurrentRetryInterval?.ToTimeSpan();
            ExpirationTime = info.ExpirationTime?.ToDateTime();
            HeartbeatTimeout = info.HeartbeatTimeout?.ToTimeSpan();
            LastAttemptCompleteTime = info.LastAttemptCompleteTime?.ToDateTime();
            LastDeploymentVersion = info.LastDeploymentVersion == null ? null : WorkerDeploymentVersion.FromProto(info.LastDeploymentVersion);
            LastHeartbeatTime = info.LastHeartbeatTime?.ToDateTime();
            LastStartedTime = info.LastStartedTime?.ToDateTime();
            LastWorkerIdentity = string.IsNullOrEmpty(info.LastWorkerIdentity) ? null : info.LastWorkerIdentity;
            NextAttemptScheduleTime = info.NextAttemptScheduleTime?.ToDateTime();
            Priority = new(info.Priority);
            RetryPolicy = info.RetryPolicy == null ? null : RetryPolicy.FromProto(info.RetryPolicy);
            RunState = info.RunState;
            ScheduleToCloseTimeout = info.ScheduleToCloseTimeout?.ToTimeSpan();
            ScheduleToStartTimeout = info.ScheduleToStartTimeout?.ToTimeSpan();
            StartDelay = info.StartDelay?.ToTimeSpan();
            StartToCloseTimeout = info.StartToCloseTimeout?.ToTimeSpan();
            TotalHeartbeatCount = info.TotalHeartbeatCount;
#pragma warning disable VSTHRD011 // This should not be able to deadlock
            userMetadata = new(() => dataConverter.FromUserMetadataAsync(info.UserMetadata));
            lastFailure = new(async () => info.LastFailure == null ? null : await dataConverter.ToExceptionAsync(info.LastFailure.Clone()).ConfigureAwait(false));
            outcomeFailure = new(async () => resp.Outcome?.Failure == null ? null : await dataConverter.ToExceptionAsync(resp.Outcome.Failure.Clone()).ConfigureAwait(false));
#pragma warning restore VSTHRD011
        }

        /// <summary>
        /// Gets the current attempt number, starting at 1.
        /// </summary>
        public int Attempt { get; }

        /// <summary>
        /// Gets the reason for cancellation, if cancel was requested.
        /// </summary>
        public string? CanceledReason { get; }

        /// <summary>
        /// Gets the time until the next retry, if applicable.
        /// </summary>
        public TimeSpan? CurrentRetryInterval { get; }

        /// <summary>
        /// Gets the schedule time plus schedule-to-close timeout.
        /// </summary>
        public DateTime? ExpirationTime { get; }

        /// <summary>
        /// Gets a value indicating whether heartbeat details are available.
        /// </summary>
        /// <remarks>
        /// Always false if <see cref="ActivityDescribeOptions.IncludeHeartbeatDetails"/> was false.
        /// <para>Heartbeat details payloads can be accessed via <c>RawInfo.HeartbeatDetails.Payloads_</c>.</para>
        /// </remarks>
        public bool HasHeartbeatDetails => requestOptions.IncludeHeartbeatDetails && RawInfo.HeartbeatDetails?.Payloads_?.Count > 0;

        /// <summary>
        /// Gets a value indicating whether the activity input is available.
        /// </summary>
        /// <remarks>
        /// Always false if <see cref="ActivityDescribeOptions.IncludeInput"/> was false.
        /// <para>Input payloads can be accessed via <see cref="RawInput"/>.</para>
        /// </remarks>
        public bool HasInput => requestOptions.IncludeInput && RawInput?.Count > 0;

        /// <summary>
        /// Gets a value indicating whether the last failure is available.
        /// </summary>
        /// <remarks>Always false if <see cref="ActivityDescribeOptions.IncludeLastFailure"/> was false.</remarks>
        /// <seealso cref="GetLastFailureAsync"/>
        public bool HasLastFailure => requestOptions.IncludeLastFailure && RawInfo.LastFailure != null;

        /// <summary>
        /// Gets a value indicating whether the activity result is available.
        /// </summary>
        /// <remarks>
        /// Activity result is only available if the activity has completed successfully.
        /// <para>Always false if <see cref="ActivityDescribeOptions.IncludeOutcome"/> was false.</para>
        /// </remarks>
        /// <seealso cref="GetResultAsync"/>
        /// <seealso cref="HasOutcomeFailure"/>
        public bool HasResult => requestOptions.IncludeOutcome && RawOutcome?.Result?.Payloads_.Count == 1; // 0 means missing, more than 1 is invalid

        /// <summary>
        /// Gets a value indicating whether the outcome failure is available.
        /// </summary>
        /// <remarks>
        /// Activity outcome failure is only available if the activity has closed with a failure.
        /// <para>Always false if <see cref="ActivityDescribeOptions.IncludeOutcome"/> was false.</para>
        /// </remarks>
        /// <seealso cref="GetOutcomeFailureAsync"/>
        /// <seealso cref="HasResult"/>
        public bool HasOutcomeFailure => requestOptions.IncludeOutcome && RawOutcome?.Failure != null;

        /// <summary>
        /// Gets the heartbeat timeout.
        /// </summary>
        public TimeSpan? HeartbeatTimeout { get; }

        /// <summary>
        /// Gets when the last attempt completed.
        /// </summary>
        public DateTime? LastAttemptCompleteTime { get; }

        /// <summary>
        /// Gets the worker deployment version this activity was dispatched to most recently.
        /// </summary>
        public WorkerDeploymentVersion? LastDeploymentVersion { get; }

        /// <summary>
        /// Gets the time of the last heartbeat.
        /// </summary>
        public DateTime? LastHeartbeatTime { get; }

        /// <summary>
        /// Gets when the last attempt was started.
        /// </summary>
        public DateTime? LastStartedTime { get; }

        /// <summary>
        /// Gets the identity of the last worker that processed the activity.
        /// </summary>
        public string? LastWorkerIdentity { get; }

        /// <summary>
        /// Gets when the next attempt will be scheduled.
        /// </summary>
        public DateTime? NextAttemptScheduleTime { get; }

        /// <summary>
        /// Gets the priority metadata.
        /// </summary>
        public Priority Priority { get; }

        /// <summary>
        /// Gets the retry policy for the activity.
        /// </summary>
        public RetryPolicy? RetryPolicy { get; }

        /// <summary>
        /// Gets the more detailed run state if the activity status is running.
        /// </summary>
        public PendingActivityState RunState { get; }

        /// <summary>
        /// Gets the schedule-to-close timeout.
        /// </summary>
        public TimeSpan? ScheduleToCloseTimeout { get; }

        /// <summary>
        /// Gets the schedule-to-start timeout.
        /// </summary>
        public TimeSpan? ScheduleToStartTimeout { get; }

        /// <summary>
        /// Gets the time to wait before making the first activity task available for dispatch.
        /// </summary>
        public TimeSpan? StartDelay { get; }

        /// <summary>
        /// Gets the start-to-close timeout.
        /// </summary>
        public TimeSpan? StartToCloseTimeout { get; }

        /// <summary>
        /// Gets the total number of heartbeats recorded across all attempts of this activity, including retries.
        /// <para>
        /// Zero if the activity has not sent any heartbeats or if the server didn't report heartbeat count.
        /// </para>
        /// </summary>
        public long TotalHeartbeatCount { get; }

        /// <summary>
        /// Gets the raw proto info.
        /// </summary>
        public new ActivityExecutionInfo RawInfo => RawResponse.Info;

        /// <summary>
        /// Gets the raw proto input.
        /// </summary>
        public IReadOnlyCollection<Payload>? RawInput => RawResponse.Input?.Payloads_;

        /// <summary>
        /// Gets the raw proto outcome.
        /// </summary>
        public ActivityExecutionOutcome? RawOutcome => RawResponse.Outcome;

        /// <summary>
        /// Gets the raw proto description.
        /// </summary>
        internal DescribeActivityExecutionResponse RawResponse { get; }

#pragma warning disable VSTHRD003 // Awaiting our own lazily-created tasks
        /// <summary>
        /// Gets the single-line fixed summary for this activity execution that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        /// <returns>Activity summary.</returns>
        public async Task<string?> GetSummaryAsync() =>
            (await userMetadata.Value.ConfigureAwait(false)).Summary;

        /// <summary>
        /// Gets the general fixed details for this activity execution that may appear in UI/CLI.
        /// This can be in Temporal markdown format and can span multiple lines.
        /// </summary>
        /// <remarks>WARNING: This method is experimental.</remarks>
        /// <returns>Static details.</returns>
        public async Task<string?> GetStaticDetailsAsync() =>
            (await userMetadata.Value.ConfigureAwait(false)).Details;

        /// <summary>
        /// Gets the failure from the last failed attempt, or null if not available.
        /// </summary>
        /// <returns>Last failure, or null if not available.</returns>
        /// <exception cref="InvalidOperationException">If <see cref="ActivityDescribeOptions.IncludeLastFailure"/> was false.</exception>
        /// <seealso cref="HasLastFailure"/>
        public async Task<Exception?> GetLastFailureAsync()
        {
            if (!requestOptions.IncludeLastFailure)
            {
                throw new InvalidOperationException("ActivityDescribeOptions.IncludeLastFailure must be set to true.");
            }
            return await lastFailure.Value.ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the failure of the activity execution, or null if not available.
        /// </summary>
        /// <returns>Activity outcome failure, or null if not available.</returns>
        /// <exception cref="InvalidOperationException">If <see cref="ActivityDescribeOptions.IncludeLastFailure"/> was false.</exception>
        /// <remarks>Activity outcome failure is only available if the activity has closed with a failure.</remarks>
        /// <seealso cref="GetResultAsync"/>
        /// <seealso cref="HasOutcomeFailure"/>
        public async Task<Exception?> GetOutcomeFailureAsync()
        {
            if (!requestOptions.IncludeOutcome)
            {
                throw new InvalidOperationException("ActivityDescribeOptions.IncludeOutcome must be set to true.");
            }
            return await outcomeFailure.Value.ConfigureAwait(false);
        }
#pragma warning restore VSTHRD003

        /// <summary>
        /// Gets the result of the activity execution.
        /// </summary>
        /// <typeparam name="T">Type to convert the result into.</typeparam>
        /// <returns>Activity result.</returns>
        /// <exception cref="InvalidOperationException">If result is not available. See <see cref="HasResult"/>.</exception>
        /// <exception cref="InvalidOperationException">If <see cref="ActivityDescribeOptions.IncludeOutcome"/> was false.</exception>
        /// <remarks>Activity result is only available if the activity has completed successfully.</remarks>
        /// <seealso cref="GetOutcomeFailureAsync"/>
        /// <seealso cref="HasResult"/>
        public async Task<T> GetResultAsync<T>()
        {
            if (!requestOptions.IncludeOutcome)
            {
                throw new InvalidOperationException("ActivityDescribeOptions.IncludeOutcome must be set to true.");
            }
            if (!HasResult)
            {
                throw new InvalidOperationException("Result unavailable.");
            }
            return await dataConverter.ToSingleValueAsync<T>(RawOutcome!.Result.Payloads_).ConfigureAwait(false);
        }
    }
}
