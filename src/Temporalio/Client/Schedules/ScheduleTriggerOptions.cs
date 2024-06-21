using Temporalio.Api.Enums.V1;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Options for triggering a schedule.
    /// </summary>
    public class ScheduleTriggerOptions
    {
        /// <summary>
        /// Gets or sets a value overriding the schedule overlap policy if set.
        /// </summary>
        public ScheduleOverlapPolicy Overlap { get; set; } = ScheduleOverlapPolicy.Unspecified;

        /// <summary>
        /// Gets or sets RPC options for triggering the schedule.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ScheduleTriggerOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}