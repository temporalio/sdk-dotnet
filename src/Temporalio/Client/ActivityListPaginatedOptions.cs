using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="ITemporalClient.ListActivitiesPaginatedAsync"/>.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityListPaginatedOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the number of results per page. Zero means server default.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets RPC options for listing activities.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityListPaginatedOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
