using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Information about a listed schedule.
    /// </summary>
    /// <param name="RecentActions">Most recent actions, oldest first. This may be a smaller count
    /// than <see cref="ScheduleInfo.RecentActions" />.</param>
    /// <param name="NextActionTimes">Next scheduled action times. This may be a smaller count than
    /// <see cref="ScheduleInfo.NextActionTimes" />.</param>
    public record ScheduleListInfo(
        IReadOnlyCollection<ScheduleActionResult> RecentActions,
        IReadOnlyCollection<DateTime> NextActionTimes)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleListInfo FromProto(Api.Schedule.V1.ScheduleListInfo proto) => new(
            RecentActions: proto.RecentActions.Select(ScheduleActionResult.FromProto).ToList(),
            NextActionTimes: proto.FutureActionTimes.Select(t => t.ToDateTime()).ToList());
    }
}