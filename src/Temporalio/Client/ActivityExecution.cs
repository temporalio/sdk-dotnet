using System;
using System.Threading;
using Temporalio.Api.Activity.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a standalone activity execution from a list call.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityExecution
    {
        private readonly Lazy<SearchAttributeCollection> searchAttributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityExecution"/> class from list info.
        /// </summary>
        /// <param name="rawInfo">Raw proto list info.</param>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
        protected internal ActivityExecution(
            ActivityExecutionListInfo rawInfo, string clientNamespace)
            : this(
                clientNamespace: clientNamespace,
                activityId: rawInfo.ActivityId,
                activityRunId: string.IsNullOrEmpty(rawInfo.RunId) ? null : rawInfo.RunId,
                activityType: rawInfo.ActivityType?.Name ?? string.Empty,
                closeTime: rawInfo.CloseTime?.ToDateTime(),
                executionDuration: rawInfo.ExecutionDuration?.ToTimeSpan(),
                scheduledTime: rawInfo.ScheduleTime?.ToDateTime() ?? default,
                stateTransitionCount: rawInfo.StateTransitionCount,
                status: rawInfo.Status,
                taskQueue: rawInfo.TaskQueue,
                searchAttributesFactory: () => rawInfo.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(rawInfo.SearchAttributes))
        {
            RawInfo = rawInfo;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityExecution"/> class.
        /// </summary>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="activityId">Activity ID.</param>
        /// <param name="activityRunId">Activity run ID.</param>
        /// <param name="activityType">Activity type name.</param>
        /// <param name="closeTime">Close time.</param>
        /// <param name="executionDuration">Execution duration.</param>
        /// <param name="scheduledTime">Scheduled time.</param>
        /// <param name="stateTransitionCount">State transition count.</param>
        /// <param name="status">Activity status.</param>
        /// <param name="taskQueue">Task queue.</param>
        /// <param name="searchAttributesFactory">Factory for lazy search attribute creation.</param>
        private protected ActivityExecution(
            string clientNamespace,
            string activityId,
            string? activityRunId,
            string activityType,
            DateTime? closeTime,
            TimeSpan? executionDuration,
            DateTime scheduledTime,
            long stateTransitionCount,
            ActivityExecutionStatus status,
            string taskQueue,
            Func<SearchAttributeCollection> searchAttributesFactory)
        {
            Namespace = clientNamespace;
            ActivityId = activityId;
            ActivityRunId = activityRunId;
            ActivityType = activityType;
            CloseTime = closeTime;
            ExecutionDuration = executionDuration;
            ScheduledTime = scheduledTime;
            StateTransitionCount = stateTransitionCount;
            Status = status;
            TaskQueue = taskQueue;
            searchAttributes = new(searchAttributesFactory, LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Gets the activity ID.
        /// </summary>
        public string ActivityId { get; private init; }

        /// <summary>
        /// Gets the activity run ID.
        /// </summary>
        public string? ActivityRunId { get; private init; }

        /// <summary>
        /// Gets the activity type name.
        /// </summary>
        public string ActivityType { get; private init; }

        /// <summary>
        /// Gets when the activity was closed if in a terminal state.
        /// </summary>
        public DateTime? CloseTime { get; private init; }

        /// <summary>
        /// Gets the total execution duration if the activity is closed.
        /// </summary>
        public TimeSpan? ExecutionDuration { get; private init; }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace { get; private init; }

        /// <summary>
        /// Gets when the activity was originally scheduled.
        /// </summary>
        public DateTime ScheduledTime { get; private init; }

        /// <summary>
        /// Gets the number of state transitions.
        /// </summary>
        public long StateTransitionCount { get; private init; }

        /// <summary>
        /// Gets the status of the activity.
        /// </summary>
        public ActivityExecutionStatus Status { get; private init; }

        /// <summary>
        /// Gets the task queue for the activity.
        /// </summary>
        public string TaskQueue { get; private init; }

        /// <summary>
        /// Gets the search attributes on the activity.
        /// </summary>
        /// <remarks>This is lazily converted on first access.</remarks>
        public SearchAttributeCollection TypedSearchAttributes => searchAttributes.Value;

        /// <summary>
        /// Gets the raw proto list info, or null if this was created from a describe call.
        /// </summary>
        internal ActivityExecutionListInfo? RawInfo { get; private init; }
    }
}
