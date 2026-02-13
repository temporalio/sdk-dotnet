using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Options for canceling a standalone activity.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityCancelOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the reason for the cancellation request.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets RPC options for canceling the activity.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ActivityCancelOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
