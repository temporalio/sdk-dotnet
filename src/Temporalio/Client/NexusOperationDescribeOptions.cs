using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for describing a standalone Nexus operation.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationDescribeOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets a long-poll token from a previous describe response. When set, the
        /// describe call will long-poll for state changes.
        /// </summary>
        public byte[]? LongPollToken { get; set; }

        /// <summary>
        /// Gets or sets RPC options for describing the operation.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (NexusOperationDescribeOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
