using System;
using Temporalio.Api.Enums.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for starting a standalone activity. <see cref="Id" /> and <see cref="TaskQueue" />
    /// are required.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class StartActivityOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StartActivityOptions"/> class.
        /// </summary>
        public StartActivityOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StartActivityOptions"/> class.
        /// </summary>
        /// <param name="id">Activity ID.</param>
        /// <param name="taskQueue">Task queue to start the activity on.</param>
        public StartActivityOptions(string id, string taskQueue)
        {
            Id = id;
            TaskQueue = taskQueue;
        }

        /// <summary>
        /// Gets or sets the unique activity identifier. This is required.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the task queue to run the activity on. This is required.
        /// </summary>
        public string? TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets the total time the activity is allowed to run including retries.
        /// </summary>
        public TimeSpan? ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum time the activity can wait in the task queue before being
        /// picked up by a worker. This timeout is non-retryable.
        /// </summary>
        public TimeSpan? ScheduleToStartTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum time for a single execution attempt. This timeout is
        /// retryable.
        /// </summary>
        public TimeSpan? StartToCloseTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum time between successful heartbeats.
        /// </summary>
        public TimeSpan? HeartbeatTimeout { get; set; }

        /// <summary>
        /// Gets or sets whether to allow re-using an activity ID from a previously *closed*
        /// activity. Default is <see cref="ActivityIdReusePolicy.AllowDuplicate" />.
        /// </summary>
        public ActivityIdReusePolicy IdReusePolicy { get; set; } = ActivityIdReusePolicy.AllowDuplicate;

        /// <summary>
        /// Gets or sets how already-running activities of the same ID are treated. Default is
        /// <see cref="ActivityIdConflictPolicy.Unspecified" /> which effectively means
        /// <see cref="ActivityIdConflictPolicy.Fail" /> on the server.
        /// </summary>
        public ActivityIdConflictPolicy IdConflictPolicy { get; set; } = ActivityIdConflictPolicy.Unspecified;

        /// <summary>
        /// Gets or sets the retry policy for the activity. If unset, uses server default.
        /// </summary>
        public RetryPolicy? RetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets the search attributes for the activity.
        /// </summary>
        public SearchAttributeCollection? TypedSearchAttributes { get; set; }

        /// <summary>
        /// Gets or sets a single-line fixed summary for this activity execution that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticSummary { get; set; }

        /// <summary>
        /// Gets or sets general fixed details for this activity execution that may appear in
        /// UI/CLI. This can be in Temporal markdown format and can span multiple lines.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticDetails { get; set; }

        /// <summary>
        /// Gets or sets the priority to use when starting this activity.
        /// </summary>
        public Priority? Priority { get; set; }

        /// <summary>
        /// Gets or sets RPC options for starting the activity.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (StartActivityOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
