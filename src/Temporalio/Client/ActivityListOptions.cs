#if NETCOREAPP3_0_OR_GREATER
using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="ITemporalClient.ListActivitiesAsync" />.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityListOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets RPC options for listing activities.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of activities to return. A zero value means no limit.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityListOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
#endif
