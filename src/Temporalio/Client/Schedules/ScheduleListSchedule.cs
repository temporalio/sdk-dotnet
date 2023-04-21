namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Details for a listed schedule.
    /// </summary>
    /// <param name="Action">Action taken when scheduled.</param>
    /// <param name="Spec">When the action is taken.</param>
    /// <param name="State">State of the schedule.</param>
    public record ScheduleListSchedule(
        ScheduleListAction Action,
        ScheduleSpec Spec,
        ScheduleListState State)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleListSchedule FromProto(Api.Schedule.V1.ScheduleListInfo proto) =>
            new(
                Action: ScheduleListAction.FromProto(proto),
                Spec: ScheduleSpec.FromProto(proto.Spec),
                State: ScheduleListState.FromProto(proto));
    }
}