using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="ActivityHandle.RestoreOriginalOptionsAsync"/>.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityRestoreOriginalOptionsOptions : ICloneable
    {
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
            var copy = (ActivityRestoreOriginalOptionsOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
