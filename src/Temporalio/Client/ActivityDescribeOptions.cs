using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for describing a standalone activity.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityDescribeOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets a value indicating whether to include input in the response if available.
        /// </summary>
        /// <seealso cref="ActivityExecutionDescription.HasInput"/>
        public bool IncludeInput { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include outcome in the response if available.
        /// </summary>
        /// <seealso cref="ActivityExecutionDescription.HasResult"/>
        /// <seealso cref="ActivityExecutionDescription.HasOutcomeFailure"/>
        /// <seealso cref="ActivityExecutionDescription.GetResultAsync"/>
        /// <seealso cref="ActivityExecutionDescription.GetOutcomeFailureAsync"/>
        public bool IncludeOutcome { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include heartbeat details in the response if available.
        /// </summary>
        /// <seealso cref="ActivityExecutionDescription.HasHeartbeatDetails"/>
        public bool IncludeHeartbeatDetails { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include last failure in the response if available.
        /// </summary>
        /// <seealso cref="ActivityExecutionDescription.HasLastFailure"/>
        /// <seealso cref="ActivityExecutionDescription.GetLastFailureAsync"/>
        public bool IncludeLastFailure { get; set; }

        /// <summary>
        /// Gets or sets RPC options for describing the activity.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityDescribeOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
