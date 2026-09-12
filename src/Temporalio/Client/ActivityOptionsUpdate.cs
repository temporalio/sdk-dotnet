#pragma warning disable SA1402 // We allow same-named types in the same file

using System;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Represents an individual change to one activity option - either setting it to a value or unsetting (clearing)
    /// it. Instances are created by calling the <see cref="OptionKey{T}.ValueSet">ValueSet</see> or
    /// <see cref="OptionKey{T}.ValueUnset">ValueUnset</see> method of the corresponding option key.
    /// All keys are static properties of this class.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    /// <seealso cref="ActivityHandle.UpdateOptionsAsync"/>
    public class ActivityOptionsUpdate
    {
        private object? value;
        private Action<Api.Activity.V1.ActivityOptions> apply;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityOptionsUpdate"/> class.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="hasValue">True if update is a set, false if update is an unset.</param>
        /// <param name="value">Value to set.</param>
        /// <param name="apply">Function that sets the value in Proto options.</param>
        private ActivityOptionsUpdate(OptionKey key, bool hasValue, object? value, Action<Api.Activity.V1.ActivityOptions> apply)
        {
            Key = key;
            HasValue = hasValue;
            this.value = value;
            this.apply = apply;
        }

        /// <summary>
        /// Gets the key for setting task queue. Cannot be unset.
        /// </summary>
        /// <seealso cref="StartActivityOptions.TaskQueue"/>
        public static OptionKey<string> TaskQueue { get; } = new(
            "task_queue.name", (o, v) => o.TaskQueue = new() { Name = v });

        /// <summary>
        /// Gets the key for setting schedule-to-close timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.ScheduleToCloseTimeout"/>
        public static OptionKey<TimeSpan> ScheduleToCloseTimeout { get; } = new(
            "schedule_to_close_timeout",
            (o, v) => o.ScheduleToCloseTimeout = Duration.FromTimeSpan(v));

        /// <summary>
        /// Gets the key for setting schedule-to-start timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.ScheduleToStartTimeout"/>
        public static OptionKey<TimeSpan> ScheduleToStartTimeout { get; } = new(
            "schedule_to_start_timeout",
            (o, v) => o.ScheduleToStartTimeout = Duration.FromTimeSpan(v));

        /// <summary>
        /// Gets the key for setting start-to-close timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.StartToCloseTimeout"/>
        public static OptionKey<TimeSpan> StartToCloseTimeout { get; } = new(
            "start_to_close_timeout",
            (o, v) => o.StartToCloseTimeout = Duration.FromTimeSpan(v));

        /// <summary>
        /// Gets the key for setting heartbeat timeout.
        /// </summary>
        /// <seealso cref="StartActivityOptions.HeartbeatTimeout"/>
        public static OptionKey<TimeSpan> HeartbeatTimeout { get; } = new(
            "heartbeat_timeout",
            (o, v) => o.HeartbeatTimeout = Duration.FromTimeSpan(v));

        /// <summary>
        /// Gets the key for setting retry policy.
        /// </summary>
        /// <remarks>
        /// If value is set for this key, it will replace the entire policy, and properties with zero value will be
        /// given default values by the server.
        /// </remarks>
        /// <seealso cref="StartActivityOptions.RetryPolicy"/>
        public static OptionKey<RetryPolicy> RetryPolicy { get; } = new(
            "retry_policy",
            (o, v) => o.RetryPolicy = v.ToProto());

        /// <summary>
        /// Gets the key for setting priority.
        /// </summary>
        /// <remarks>
        /// If value is set for this key, it will replace the entire priority object, and any unset properties will be
        /// updated to null.
        /// </remarks>
        /// <seealso cref="StartActivityOptions.Priority"/>
        public static OptionKey<Priority> Priority { get; } = new(
            "priority",
            (o, v) => o.Priority = v.ToProto());

        /// <summary>
        /// Gets the key for setting start delay.
        /// </summary>
        /// <seealso cref="StartActivityOptions.StartDelay"/>
        public static OptionKey<TimeSpan> StartDelay { get; } = new(
            "start_delay",
            (o, v) => o.StartDelay = Duration.FromTimeSpan(v));

        /// <summary>
        /// Gets the option key of this update object. Every key is a static property of this class.
        /// </summary>
        public OptionKey Key { get; }

        /// <summary>
        /// Gets a value indicating whether this updates sets or unsets the associated option.
        /// True if sets, false if unsets.
        /// </summary>
        public bool HasValue { get; }

        /// <summary>
        /// Gets the value this update will set the associated option to.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// If this update is an unset (<see cref="HasValue"/> is false).
        /// </exception>
        public object Value => HasValue ? value! : throw new InvalidOperationException("No value");

        /// <summary>
        /// Applies the update to the given options object.
        /// </summary>
        /// <param name="options">Proto options.</param>
        internal void Apply(Api.Activity.V1.ActivityOptions options) => apply(options);

        /// <summary>
        /// Non-generic base class for <see cref="OptionKey{T}"/>. All keys are static properties of
        /// <see cref="ActivityOptionsUpdate"/>.
        /// </summary>
        /// <seealso cref="OptionKey{T}"/>
        /// <seealso cref="ActivityOptionsUpdate"/>
        public class OptionKey
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="OptionKey"/> class.
            /// </summary>
            /// <param name="path">Protobuf field mask path.</param>
            private protected OptionKey(string path)
            {
                Path = path;
            }

            /// <summary>
            /// Gets the Protobuf field mask path.
            /// </summary>
            internal string Path { get; }

            /// <inheritdoc/>
            public override string ToString() => Path;
        }

        /// <summary>
        /// Option key for activity options update operation. All keys are static properties of
        /// <see cref="ActivityOptionsUpdate"/>.
        /// </summary>
        /// <typeparam name="T">Value type of the associated option.</typeparam>
        /// <seealso cref="ActivityOptionsUpdate"/>
        public class OptionKey<T> : OptionKey
        {
            private readonly Action<Api.Activity.V1.ActivityOptions, T> apply;

            /// <summary>
            /// Initializes a new instance of the <see cref="OptionKey{T}"/> class.
            /// </summary>
            /// <param name="path">Protobuf field mask path.</param>
            /// <param name="apply">Function that sets the provided value in the Protobuf options object.</param>
            internal OptionKey(string path, Action<Api.Activity.V1.ActivityOptions, T> apply)
                : base(path)
            {
                this.apply = apply;
            }

            /// <summary>
            /// Creates an update object that will set the associated option.
            /// </summary>
            /// <param name="value">Value to set.</param>
            /// <returns>Update object.</returns>
            /// <seealso cref="ActivityHandle.UpdateOptionsAsync"/>
            public ActivityOptionsUpdate ValueSet(T value) => new(this, true, value, options => apply(options, value));

            /// <summary>
            /// Creates an update object that will unset the associated option.
            /// </summary>
            /// <returns>Update object.</returns>
            /// <seealso cref="ActivityHandle.UpdateOptionsAsync"/>
            public ActivityOptionsUpdate ValueUnset() => new(this, false, null, _ => { });
        }
    }
}
