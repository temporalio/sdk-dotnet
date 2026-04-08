using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for listing standalone Nexus operations with manual pagination.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationListPaginatedOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the page size. 0 or negative means server default.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets RPC options for listing operations.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationListPaginatedOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
