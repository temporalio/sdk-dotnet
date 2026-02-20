using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for terminating a standalone activity.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityTerminateOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the reason for the termination.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets RPC options for terminating the activity.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityTerminateOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
