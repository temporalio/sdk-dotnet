using System;
using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="ActivityHandle.UpdateOptionsAsync"/>.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityUpdateOptionsOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the options updates to perform. Must be non-empty.
        /// </summary>
        public IReadOnlyCollection<ActivityOptionsUpdate>? Updates { get; set; }

        /// <summary>
        /// Gets or sets RPC options for unpausing the activity.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityUpdateOptionsOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
