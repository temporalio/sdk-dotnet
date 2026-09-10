using System;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Contains current activity options after applying an update. Returned by
    /// <see cref="ActivityHandle.UpdateOptionsAsync"/> and <see cref="ActivityHandle.RestoreOriginalOptionsAsync"/>.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public record ActivityUpdateOptionsResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityUpdateOptionsResult"/> class.
        /// </summary>
        /// <param name="proto">Protobuf options.</param>
        internal ActivityUpdateOptionsResult(Api.Activity.V1.ActivityOptions? proto)
        {
            TaskQueue = proto?.TaskQueue?.Name ?? string.Empty;
            ScheduleToCloseTimeout = proto?.ScheduleToCloseTimeout?.ToTimeSpan();
            ScheduleToStartTimeout = proto?.ScheduleToStartTimeout?.ToTimeSpan();
            StartToCloseTimeout = proto?.StartToCloseTimeout?.ToTimeSpan();
            HeartbeatTimeout = proto?.HeartbeatTimeout?.ToTimeSpan();
            if (proto?.RetryPolicy is { } rp)
            {
                RetryPolicy = RetryPolicy.FromProto(rp);
            }
            Priority = new Priority(proto?.Priority!);
            StartDelay = proto?.StartDelay?.ToTimeSpan();
        }

        /// <summary>
        /// Gets the task queue.
        /// </summary>
        /// <seealso cref="StartActivityOptions.TaskQueue"/>
        public string TaskQueue { get; }

        /// <summary>
        /// Gets the schedule-to-close timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.ScheduleToCloseTimeout"/>
        public TimeSpan? ScheduleToCloseTimeout { get; }

        /// <summary>
        /// Gets the schedule-to-start timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.ScheduleToStartTimeout"/>
        public TimeSpan? ScheduleToStartTimeout { get; }

        /// <summary>
        /// Gets the start-to-close timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.StartToCloseTimeout"/>
        public TimeSpan? StartToCloseTimeout { get; }

        /// <summary>
        /// Gets the heartbeat timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.HeartbeatTimeout"/>
        public TimeSpan? HeartbeatTimeout { get; }

        /// <summary>
        /// Gets the retry policy.
        /// </summary>
        /// <seealso cref="StartActivityOptions.RetryPolicy"/>
        public RetryPolicy? RetryPolicy { get; }

        /// <summary>
        /// Gets the priority.
        /// </summary>
        /// <seealso cref="StartActivityOptions.Priority"/>
        public Priority Priority { get; }

        /// <summary>
        /// Gets the start delay.
        /// </summary>
        /// <seealso cref="StartActivityOptions.StartDelay"/>
        public TimeSpan? StartDelay { get; }
    }
}
