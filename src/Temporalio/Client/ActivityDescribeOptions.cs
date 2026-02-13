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
        /// Gets or sets a long-poll token from a previous describe response. When set, the
        /// describe call will long-poll for state changes.
        /// </summary>
        public byte[]? LongPollToken { get; set; }

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
