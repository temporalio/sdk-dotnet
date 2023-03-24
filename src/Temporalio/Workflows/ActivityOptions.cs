using System;
using System.Threading;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Options for activity execution from a workflow. Either <see cref="ScheduleToCloseTimeout" />
    /// or <see cref="StartToCloseTimeout" /> must be set. <see cref="HeartbeatTimeout" /> must be
    /// set for the activity to receive cancellations and all but the most instantly completing
    /// activities should set this.
    /// </summary>
    public class ActivityOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the schedule to close timeout.
        /// </summary>
        /// <remarks>
        /// This is the timeout from schedule to completion of the activity (all attempts,
        /// including retries). Either this or <see cref="StartToCloseTimeout" /> must be set. If
        /// unset, default is the workflow execution timeout.
        /// </remarks>
        public TimeSpan? ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets the schedule to start timeout.
        /// </summary>
        /// <remarks>
        /// This is the timeout from schedule to when the activity is picked up by a worker. If
        /// unset, defaults to <see cref="ScheduleToCloseTimeout" />.
        /// </remarks>
        public TimeSpan? ScheduleToStartTimeout { get; set; }

        /// <summary>
        /// Gets or sets the start to close timeout.
        /// </summary>
        /// <remarks>
        /// This is the timeout for each separate retry attempt from start to completion of the
        /// attempt. Either this or <see cref="ScheduleToCloseTimeout" /> must be set.
        /// </remarks>
        public TimeSpan? StartToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets the heartbeat timeout.
        /// </summary>
        /// <remarks>
        /// This must be set for an activity to receive cancellation. This should always be set for
        /// all but the most instantly completing activities.
        /// </remarks>
        public TimeSpan? HeartbeatTimeout { get; set; }

        /// <summary>
        /// Gets or sets the retry policy. If unset, defaults to retrying forever.
        /// </summary>
        public RetryPolicy? RetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets how the workflow will send/wait for cancellation of the activity. Default
        /// is <see cref="ActivityCancellationType.TryCancel" />.
        /// </summary>
        public ActivityCancellationType CancellationType { get; set; } = ActivityCancellationType.TryCancel;

        /// <summary>
        /// Gets or sets the cancellation token for this activity. If unset, this defaults to the
        /// workflow cancellation token.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Gets or sets the task queue for this activity. If unset, defaults to the workflow task
        /// queue.
        /// </summary>
        public string? TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the activity. This should never be set unless
        /// users have a strong understanding of the system. Contact Temporal support to discuss the
        /// use case before setting this value.
        /// </summary>
        public string? ActivityID { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}