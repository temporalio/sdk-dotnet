using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Information about a schedule.
    /// </summary>
    /// <param name="NumActions">Number of actions taken by the schedule.</param>
    /// <param name="NumActionsMissedCatchupWindow">Number of actions skipped due to missing the
    /// catchup window.</param>
    /// <param name="NumActionsSkippedOverlap">Number of actions skipped due to overlap.</param>
    /// <param name="RunningActions">Currently running actions.</param>
    /// <param name="RecentActions">10 most recent actions, oldest first.</param>
    /// <param name="NextActionTimes">Next 10 scheduled action times.</param>
    /// <param name="CreatedAt">When the schedule was created.</param>
    /// <param name="LastUpdatedAt">When the schedule was last updated.</param>
    public record ScheduleInfo(
        long NumActions,
        long NumActionsMissedCatchupWindow,
        long NumActionsSkippedOverlap,
        IReadOnlyCollection<ScheduleActionExecution> RunningActions,
        IReadOnlyCollection<ScheduleActionResult> RecentActions,
        IReadOnlyCollection<DateTime> NextActionTimes,
        DateTime CreatedAt,
        DateTime? LastUpdatedAt)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleInfo FromProto(Api.Schedule.V1.ScheduleInfo proto) => new(
            NumActions: proto.ActionCount,
            NumActionsMissedCatchupWindow: proto.MissedCatchupWindow,
            NumActionsSkippedOverlap: proto.OverlapSkipped,
            RunningActions: proto.RunningWorkflows.Select(
                ScheduleActionExecutionStartWorkflow.FromProto).ToList(),
            RecentActions: proto.RecentActions.Select(ScheduleActionResult.FromProto).ToList(),
            NextActionTimes: proto.FutureActionTimes.Select(t => t.ToDateTime()).ToList(),
            CreatedAt: proto.CreateTime.ToDateTime(),
            LastUpdatedAt: proto.UpdateTime?.ToDateTime());
    }
}