using System;
using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="AsyncActivityHandle.FailAsync" />.
    /// </summary>
    public class AsyncActivityFailOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the details to record as the last heartbeat details.
        /// </summary>
        public IReadOnlyCollection<object?>? LastHeartbeatDetails { get; set; }

        /// <summary>
        /// Gets or sets RPC options.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (AsyncActivityFailOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}