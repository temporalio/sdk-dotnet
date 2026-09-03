using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for unpausing a standalone activity.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityUnpauseOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the reason for unpausing.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the random jitter duration.
        /// The next activity task will be scheduled after a random delay between zero and jitter duration.
        /// </summary>
        public TimeSpan? Jitter { get; set; }

        /// <summary>
        /// Gets or sets RPC options for unpausing the activity.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityUnpauseOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
