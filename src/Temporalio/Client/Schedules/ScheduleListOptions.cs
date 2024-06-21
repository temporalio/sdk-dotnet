#if NETCOREAPP3_0_OR_GREATER
using System;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Options when listing schedules.
    /// </summary>
    public class ScheduleListOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets RPC options for listing schedules.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ScheduleListOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}
#endif