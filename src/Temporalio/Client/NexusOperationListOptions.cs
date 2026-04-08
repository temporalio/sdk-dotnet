#if NETCOREAPP3_0_OR_GREATER
using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for listing standalone Nexus operations.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationListOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the maximum number of operations to return. 0 means server default.
        /// </summary>
        public int Limit { get; set; }

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
            var copy = (NexusOperationListOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
#endif
