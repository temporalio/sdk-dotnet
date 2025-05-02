namespace Temporalio.Common
{
    /// <summary>
    /// Priority contains metadata that controls relative ordering of task processing when tasks are
    /// backlogged in a queue. Initially, Priority will be used in activity and workflow task
    /// queues, which are typically where backlogs exist. Priority is (for now) attached to
    /// workflows and activities. Activities and child workflows inherit Priority from the workflow
    /// that created them, but may override fields when they are started or modified. For each field
    /// of a Priority on an activity/workflow, not present or equal to zero/empty string means to
    /// inherit the value from the calling workflow, or if there is no calling workflow, then use
    /// the default (documented on the field).
    ///
    /// The overall semantics of Priority are:
    /// 1. First, consider "priority_key": lower number goes first.
    /// (more will be added here later).
    /// </summary>
    public sealed record Priority()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Priority"/> class from a proto.
        /// </summary>
        /// <param name="priority">The proto to initialize from.</param>
        internal Priority(Temporalio.Api.Common.V1.Priority priority)
            : this() => PriorityKey = priority?.PriorityKey != 0 ? priority?.PriorityKey : null;

        /// <summary>
        /// Gets the priority key, which is a positive integer from 1 to n, where smaller integers
        /// correspond to higher priorities (tasks run sooner). In general, tasks in a queue should
        /// be processed in close to priority order, although small deviations are possible. The
        /// maximum priority value (minimum priority) is determined by server configuration, and
        /// defaults to 5.
        ///
        /// The default priority is (min+max)/2. With the default max of 5 and min of 1, that comes
        /// out to 3.
        /// </summary>
        public int? PriorityKey { get; init; }

        /// <summary>
        /// Converts this priority to a proto.
        /// </summary>
        /// <returns>The proto representation of this priority.</returns>
        internal Temporalio.Api.Common.V1.Priority ToProto() => new()
        {
            PriorityKey = PriorityKey ?? 0,
        };
    }
}
