using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for canceling a standalone Nexus operation.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationCancelOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the reason for the cancellation request.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets RPC options for canceling the operation.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationCancelOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
