using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for terminating a standalone Nexus operation.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationTerminateOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the reason for termination.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets RPC options for terminating the operation.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationTerminateOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
