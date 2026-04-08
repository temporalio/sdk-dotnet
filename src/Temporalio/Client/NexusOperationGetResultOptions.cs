using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for getting the result of a standalone Nexus operation.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationGetResultOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets RPC options for polling the operation result.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationGetResultOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
