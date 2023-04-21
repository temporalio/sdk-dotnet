namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// State of a listed schedule.
    /// </summary>
    /// <param name="Note">Human readable message for the schedule.</param>
    /// <param name="Paused">Whether the schedule is paused.</param>
    public record ScheduleListState(
        string? Note,
        bool Paused)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleListState FromProto(Api.Schedule.V1.ScheduleListInfo proto) => new(
            Note: string.IsNullOrEmpty(proto.Notes) ? null : proto.Notes,
            Paused: proto.Paused);
    }
}