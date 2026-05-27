using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for counting standalone Nexus operations.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationCountOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets RPC options for counting operations.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationCountOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
