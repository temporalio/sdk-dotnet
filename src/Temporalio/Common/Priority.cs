namespace Temporalio.Common
{
    /// <summary>
    /// Priority contains metadata that controls relative ordering of task processing when tasks are
    /// backlogged in a queue. Initially, Priority will be used in activity and workflow task queues,
    /// which are typically where backlogs exist.
    /// Priority is (for now) attached to workflows and activities. Activities and child workflows
    /// inherit Priority from the workflow that created them, but may override fields when they are
    /// started or modified. For each field of a Priority on an activity/workflow, not present or equal
    /// to zero/empty string means to inherit the value from the calling workflow, or if there is no
    /// calling workflow, then use the default (documented on the field).
    /// The overall semantics of Priority are:
    /// 1. First, consider "priority_key": lower number goes first.
    /// 2. Then, consider fairness: the fairness mechanism attempts to dispatch tasks for a given key in
    ///    proportion to its weight.
    /// </summary>
    public sealed record Priority
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Priority"/> class.
        /// </summary>
        /// <param name="priorityKey">The priority key.</param>
        /// <param name="fairnessKey">The fairness key.</param>
        /// <param name="fairnessWeight">The fairness weight.</param>
        public Priority(int? priorityKey = null, string? fairnessKey = null, float? fairnessWeight = null)
        {
            PriorityKey = priorityKey;
            FairnessKey = fairnessKey;
            FairnessWeight = fairnessWeight;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Priority"/> class from a proto.
        /// </summary>
        /// <param name="priority">The proto to initialize from.</param>
        internal Priority(Temporalio.Api.Common.V1.Priority priority)
            : this(
                priority?.PriorityKey != 0 ? priority?.PriorityKey : null,
                !string.IsNullOrEmpty(priority?.FairnessKey) ? priority?.FairnessKey : null,
                priority?.FairnessWeight != 0 ? priority?.FairnessWeight : null)
        {
        }

        /// <summary>
        /// Gets the default Priority instance.
        /// </summary>
        public static Priority Default { get; } = new();

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
        /// Gets the fairness key, which is a short string that's used as a key for a fairness
        /// balancing mechanism. It may correspond to a tenant id, or to a fixed
        /// string like "high" or "low". The default is the empty string.
        ///
        /// The fairness mechanism attempts to dispatch tasks for a given key in
        /// proportion to its weight. For example, using a thousand distinct tenant
        /// ids, each with a weight of 1.0 (the default) will result in each tenant
        /// getting a roughly equal share of task dispatch throughput.
        ///
        /// Fairness keys are limited to 64 bytes.
        /// </summary>
        public string? FairnessKey { get; init; }

        /// <summary>
        /// Gets the fairness weight which can come from multiple sources for
        /// flexibility. From highest to lowest precedence:
        /// 1. Weights for a small set of keys can be overridden in task queue
        ///    configuration with an API.
        /// 2. It can be attached to the workflow/activity in this field.
        /// 3. The default weight of 1.0 will be used.
        ///
        /// Weight values are clamped to the range [0.001, 1000].
        /// </summary>
        public float? FairnessWeight { get; init; }

        /// <summary>
        /// Converts this priority to a proto.
        /// </summary>
        /// <returns>The proto representation of this priority.</returns>
        internal Temporalio.Api.Common.V1.Priority ToProto() => new()
        {
            PriorityKey = PriorityKey ?? 0,
            FairnessKey = FairnessKey ?? string.Empty,
            FairnessWeight = FairnessWeight ?? 0f,
        };
    }
}
