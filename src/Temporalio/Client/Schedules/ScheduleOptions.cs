using System;
using System.Collections.Generic;
using Temporalio.Common;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Options for creating a schedule.
    /// </summary>
    public class ScheduleOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets a value indicating whether the schedule will be triggered immediately upon
        /// create.
        /// </summary>
        public bool TriggerImmediately { get; set; }

        /// <summary>
        /// Gets or sets the time periods to take actions on as if that time passed right now.
        /// </summary>
        public IReadOnlyCollection<ScheduleBackfill> Backfills { get; set; } =
            Array.Empty<ScheduleBackfill>();

        /// <summary>
        /// Gets or sets the memo for the schedule. Values for the memo cannot be null.
        /// </summary>
        public IReadOnlyDictionary<string, object>? Memo { get; set; }

        /// <summary>
        /// Gets or sets the search attributes for the schedule.
        /// </summary>
        public SearchAttributeCollection? TypedSearchAttributes { get; set; }

        /// <summary>
        /// Gets or sets RPC options for creating the schedule.
        /// </summary>
        public RpcOptions? Rpc { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (ScheduleOptions)MemberwiseClone();
            if (Rpc != null)
            {
                copy.Rpc = (RpcOptions)Rpc.Clone();
            }
            return copy;
        }
    }
}