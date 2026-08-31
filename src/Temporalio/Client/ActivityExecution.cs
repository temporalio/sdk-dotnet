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
                executionTime: rawInfo.ExecutionTime?.ToDateTime(),
                scheduleTime: rawInfo.ScheduleTime?.ToDateTime() ?? default,
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
        /// <param name="executionTime">Execution time.</param>
        /// <param name="scheduleTime">Schedule time.</param>
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
            DateTime? executionTime,
            DateTime scheduleTime,
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
            ExecutionTime = executionTime;
            ScheduleTime = scheduleTime;
            Status = status;
            TaskQueue = taskQueue;
            searchAttributes = new(searchAttributesFactory, LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Gets the activity ID.
        /// </summary>
        public string ActivityId { get; }

        /// <summary>
        /// Gets the activity run ID.
        /// </summary>
        public string? ActivityRunId { get; }

        /// <summary>
        /// Gets the activity type name.
        /// </summary>
        public string ActivityType { get; }

        /// <summary>
        /// Gets when the activity was closed if in a terminal state.
        /// </summary>
        public DateTime? CloseTime { get; }

        /// <summary>
        /// Gets the total execution duration if the activity is closed.
        /// </summary>
        public TimeSpan? ExecutionDuration { get; }

        /// <summary>
        /// Gets the time at which the first activity task is made available for dispatch,
        /// computed as schedule time + start delay.
        /// </summary>
        public DateTime? ExecutionTime { get; }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// Gets when the activity was originally scheduled.
        /// </summary>
        public DateTime ScheduleTime { get; }

        /// <summary>
        /// Gets the status of the activity.
        /// </summary>
        public ActivityExecutionStatus Status { get; }

        /// <summary>
        /// Gets the task queue for the activity.
        /// </summary>
        public string TaskQueue { get; }

        /// <summary>
        /// Gets the search attributes on the activity.
        /// </summary>
        /// <remarks>This is lazily converted on first access.</remarks>
        public SearchAttributeCollection TypedSearchAttributes => searchAttributes.Value;

        /// <summary>
        /// Gets the raw proto list info, or null if this was created from a describe call.
        /// </summary>
        /// <seealso cref="ActivityExecutionDescription.RawInfo"/>
        internal ActivityExecutionListInfo? RawInfo { get; }
    }
}
