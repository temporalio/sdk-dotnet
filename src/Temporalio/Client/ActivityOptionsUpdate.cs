using System;
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Api.Activity.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Activity execution options that can be changed after activity is started.
    /// </summary>
    /// <remarks>
    /// Used as both the argument and the return value of <see cref="ActivityHandle.UpdateOptionsAsync"/>.
    /// When used as an argument, only options that are set to non-null values are updated.
    /// Options with null values are left unchanged unless they are explicitly marked to be cleared.
    ///
    /// WARNING: Standalone activities are experimental.
    /// </remarks>
    public class ActivityOptionsUpdate : ICloneable
    {
        private const string PathTaskQueue = "task_queue.name";
        private const string PathScheduleToCloseTimeout = "schedule_to_close_timeout";
        private const string PathScheduleToStartTimeout = "schedule_to_start_timeout";
        private const string PathStartToCloseTimeout = "start_to_close_timeout";
        private const string PathHeartbeatTimeout = "heartbeat_timeout";
        private const string PathRetryPolicy = "retry_policy";
        private const string PathPriority = "priority";
        private const string PathStartDelay = "start_delay";

        private string? taskQueue;
        private TimeSpan? scheduleToCloseTimeout;
        private TimeSpan? scheduleToStartTimeout;
        private TimeSpan? startToCloseTimeout;
        private TimeSpan? heartbeatTimeout;
        private RetryPolicy? retryPolicy;
        private Priority? priority;
        private TimeSpan? startDelay;

        private HashSet<string> paths = new();

        /// <summary>
        /// Gets or sets the task queue to run the activity on.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated.
        /// Task queue is required and cannot be cleared.
        /// </remarks>
        public string? TaskQueue
        {
            get => taskQueue;
            set => SetRef(PathTaskQueue, out taskQueue, value);
        }

        /// <summary>
        /// Gets or sets the total time the activity is allowed to run including retries.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated (<see cref="ClearScheduleToCloseTimeout"/> is set to false).
        /// To clear the current value, set <see cref="ClearScheduleToCloseTimeout"/> to true.
        /// </remarks>
        public TimeSpan? ScheduleToCloseTimeout
        {
            get => scheduleToCloseTimeout;
            set => SetVal(PathScheduleToCloseTimeout, out scheduleToCloseTimeout, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="ScheduleToCloseTimeout"/> should be cleared.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="ScheduleToCloseTimeout"/> to any value, including null, sets this property to false.
        /// Setting this property to true sets <see cref="ScheduleToCloseTimeout"/> to null.
        /// </remarks>
        public bool ClearScheduleToCloseTimeout
        {
            get => scheduleToCloseTimeout == null && paths.Contains(PathScheduleToCloseTimeout);
            set => SetClearVal(PathScheduleToCloseTimeout, ref scheduleToCloseTimeout, value);
        }

        /// <summary>
        /// Gets or sets the maximum time the activity can wait in the task queue before being picked up by a worker.
        /// This timeout is non-retryable.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated (<see cref="ClearScheduleToStartTimeout"/> is set to false).
        /// To clear the current value, set <see cref="ClearScheduleToStartTimeout"/> to true.
        /// </remarks>
        public TimeSpan? ScheduleToStartTimeout
        {
            get => scheduleToStartTimeout;
            set => SetVal(PathScheduleToStartTimeout, out scheduleToStartTimeout, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="ScheduleToStartTimeout"/> should be cleared.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="ScheduleToStartTimeout"/> to any value, including null, sets this property to false.
        /// Setting this property to true sets <see cref="ScheduleToStartTimeout"/> to null.
        /// </remarks>
        public bool ClearScheduleToStartTimeout
        {
            get => scheduleToStartTimeout == null && paths.Contains(PathScheduleToStartTimeout);
            set => SetClearVal(PathScheduleToStartTimeout, ref scheduleToStartTimeout, value);
        }

        /// <summary>
        /// Gets or sets the maximum time for a single execution attempt. This timeout is retryable.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated (<see cref="ClearStartToCloseTimeout"/> is set to false).
        /// To clear the current value, set <see cref="ClearStartToCloseTimeout"/> to true.
        /// </remarks>
        public TimeSpan? StartToCloseTimeout
        {
            get => startToCloseTimeout;
            set => SetVal(PathStartToCloseTimeout, out startToCloseTimeout, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="StartToCloseTimeout"/> should be cleared.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="StartToCloseTimeout"/> to any value, including null, sets this property to false.
        /// Setting this property to true sets <see cref="StartToCloseTimeout"/> to null.
        /// </remarks>
        public bool ClearStartToCloseTimeout
        {
            get => startToCloseTimeout == null && paths.Contains(PathStartToCloseTimeout);
            set => SetClearVal(PathStartToCloseTimeout, ref startToCloseTimeout, value);
        }

        /// <summary>
        /// Gets or sets the maximum time between successful heartbeats.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated (<see cref="ClearHeartbeatTimeout"/> is set to false).
        /// To clear the current value, set <see cref="ClearHeartbeatTimeout"/> to true.
        /// </remarks>
        public TimeSpan? HeartbeatTimeout
        {
            get => heartbeatTimeout;
            set => SetVal(PathHeartbeatTimeout, out heartbeatTimeout, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="HeartbeatTimeout"/> should be cleared.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="HeartbeatTimeout"/> to any value, including null, sets this property to false.
        /// Setting this property to true sets <see cref="HeartbeatTimeout"/> to null.
        /// </remarks>
        public bool ClearHeartbeatTimeout
        {
            get => heartbeatTimeout == null && paths.Contains(PathHeartbeatTimeout);
            set => SetClearVal(PathHeartbeatTimeout, ref heartbeatTimeout, value);
        }

        /// <summary>
        /// Gets or sets the retry policy for the activity. If unset, uses server default.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated (<see cref="ClearRetryPolicy"/> is set to false).
        /// To clear the current value, set <see cref="ClearRetryPolicy"/> to true.
        /// </remarks>
        public RetryPolicy? RetryPolicy
        {
            get => retryPolicy;
            set => SetRef(PathRetryPolicy, out retryPolicy, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="RetryPolicy"/> should be cleared.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="RetryPolicy"/> to any value, including null, sets this property to false.
        /// Setting this property to true sets <see cref="RetryPolicy"/> to null.
        /// </remarks>
        public bool ClearRetryPolicy
        {
            get => retryPolicy == null && paths.Contains(PathRetryPolicy);
            set => SetClearRef(PathRetryPolicy, ref retryPolicy, value);
        }

        /// <summary>
        /// Gets or sets the priority to use when starting this activity.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated (<see cref="ClearPriority"/> is set to false).
        /// To clear the current value, set <see cref="ClearPriority"/> to true.
        /// </remarks>
        public Priority? Priority
        {
            get => priority;
            set => SetRef(PathPriority, out priority, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="Priority"/> should be cleared.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="Priority"/> to any value, including null, sets this property to false.
        /// Setting this property to true sets <see cref="Priority"/> to null.
        /// </remarks>
        public bool ClearPriority
        {
            get => priority == null && paths.Contains(PathPriority);
            set => SetClearRef(PathPriority, ref priority, value);
        }

        /// <summary>
        /// Gets or sets the time to wait before dispatching the first activity task. This delay is not applied to retry attempts.
        /// </summary>
        /// <remarks>
        /// Setting this property to null indicates it should not be updated (<see cref="ClearStartDelay"/> is set to false).
        /// To clear the current value, set <see cref="ClearStartDelay"/> to true.
        /// </remarks>
        public TimeSpan? StartDelay
        {
            get => startDelay;
            set => SetVal(PathStartDelay, out startDelay, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="StartDelay"/> should be cleared.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="StartDelay"/> to any value, including null, sets this property to false.
        /// Setting this property to true sets <see cref="StartDelay"/> to null.
        /// </remarks>
        public bool ClearStartDelay
        {
            get => startDelay == null && paths.Contains(PathStartDelay);
            set => SetClearVal(PathStartDelay, ref startDelay, value);
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityOptionsUpdate)MemberwiseClone();
            copy.paths = new HashSet<string>(paths);
            return copy;
        }

        /// <summary>
        /// Convert protobuf options to this type.
        /// </summary>
        /// <param name="proto">Protobuf options.</param>
        /// <returns>New options instance.</returns>
        internal static ActivityOptionsUpdate FromProto(ActivityOptions? proto)
        {
            ActivityOptionsUpdate options = new();

            if (proto != null)
            {
                // Using property setters to keep paths in sync. Manually checking for null to save on Remove calls.
                if (!string.IsNullOrEmpty(proto.TaskQueue?.Name))
                {
                    options.TaskQueue = proto.TaskQueue!.Name;
                }
                if (proto.ScheduleToCloseTimeout?.ToNonZeroTimeSpan() is { } s2c)
                {
                    options.ScheduleToCloseTimeout = s2c;
                }
                if (proto.ScheduleToStartTimeout?.ToNonZeroTimeSpan() is { } s2st)
                {
                    options.ScheduleToStartTimeout = s2st;
                }
                if (proto.StartToCloseTimeout?.ToNonZeroTimeSpan() is { } st2c)
                {
                    options.StartToCloseTimeout = st2c;
                }
                if (proto.HeartbeatTimeout?.ToNonZeroTimeSpan() is { } ht)
                {
                    options.HeartbeatTimeout = ht;
                }
                if (proto.RetryPolicy != null)
                {
                    options.RetryPolicy = RetryPolicy.FromProto(proto.RetryPolicy);
                }
                if (proto.Priority != null)
                {
                    options.Priority = new Priority(proto.Priority);
                }
                if (proto.StartDelay?.ToNonZeroTimeSpan() is { } sd)
                {
                    options.StartDelay = sd;
                }
            }

            return options;
        }

        /// <summary>
        /// Convert the options to their protobuf equivalent.
        /// </summary>
        /// <returns>Protobuf options.</returns>
        internal ActivityOptions ToProto()
        {
            ActivityOptions proto = new();

            if (taskQueue != null)
            {
                proto.TaskQueue = new() { Name = taskQueue };
            }
            if (scheduleToCloseTimeout is { } s2c)
            {
                proto.ScheduleToCloseTimeout = Duration.FromTimeSpan(s2c);
            }
            if (scheduleToStartTimeout is { } s2st)
            {
                proto.ScheduleToStartTimeout = Duration.FromTimeSpan(s2st);
            }
            if (startToCloseTimeout is { } st2c)
            {
                proto.StartToCloseTimeout = Duration.FromTimeSpan(st2c);
            }
            if (heartbeatTimeout is { } ht)
            {
                proto.HeartbeatTimeout = Duration.FromTimeSpan(ht);
            }
            if (retryPolicy != null)
            {
                proto.RetryPolicy = retryPolicy.ToProto();
            }
            if (priority != null)
            {
                proto.Priority = priority.ToProto();
            }
            if (startDelay is { } sd)
            {
                proto.StartDelay = Duration.FromTimeSpan(sd);
            }

            return proto;
        }

        /// <summary>
        /// Returns field mask for update options operation.
        /// </summary>
        /// <returns>Options field mask.</returns>
        internal FieldMask UpdateMask()
        {
            FieldMask mask = new();
            mask.Paths.AddRange(paths);
            return mask.Normalize();
        }

        private void SetRef<T>(string path, out T? field, T? value)
            where T : class
        {
            if (value == null)
            {
                paths.Remove(path);
                field = null;
            }
            else
            {
                paths.Add(path);
                field = value;
            }
        }

        private void SetVal<T>(string path, out T? field, T? value)
            where T : struct
        {
            if (value == null)
            {
                paths.Remove(path);
                field = null;
            }
            else
            {
                paths.Add(path);
                field = value;
            }
        }

        private void SetClearRef<T>(string path, ref T? field, bool clear)
            where T : class
        {
            if (clear)
            {
                paths.Add(path);
                field = null;
            }
            else if (field == null)
            {
                paths.Remove(path);
            }
        }

        private void SetClearVal<T>(string path, ref T? field, bool clear)
            where T : struct
        {
            if (clear)
            {
                paths.Add(path);
                field = null;
            }
            else if (field == null)
            {
                paths.Remove(path);
            }
        }
    }
}
