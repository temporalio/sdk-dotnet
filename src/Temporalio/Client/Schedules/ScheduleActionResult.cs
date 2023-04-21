using System;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Information about when an action took place.
    /// </summary>
    /// <param name="ScheduledAt">Scheduled time of the action including jitter.</param>
    /// <param name="StartedAt">When the action actually started.</param>
    /// <param name="Action">Action that took place.</param>
    public record ScheduleActionResult(
        DateTime ScheduledAt,
        DateTime StartedAt,
        ScheduleActionExecution Action)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleActionResult FromProto(
            Api.Schedule.V1.ScheduleActionResult proto) =>
            new(
                ScheduledAt: proto.ScheduleTime.ToDateTime(),
                StartedAt: proto.ActualTime.ToDateTime(),
                Action: ScheduleActionExecutionStartWorkflow.FromProto(proto.StartWorkflowResult));
    }
}