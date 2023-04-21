namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// State of a schedule.
    /// </summary>
    public record ScheduleState
    {
        /// <summary>
        /// Gets the human readable message for the schedule.
        /// </summary>
        public string? Note { get; init; }

        /// <summary>
        /// Gets a value indicating whether this schedule is paused.
        /// </summary>
        public bool Paused { get; init; }

        /// <summary>
        /// Gets a value indicating whether, if true, remaining actions will be decremented for each
        /// action taken.
        /// </summary>
        public bool LimitedActions { get; init; }

        /// <summary>
        /// Gets the actions remaining on this schedule. Once this number hits 0, no further actions
        /// are scheduled automatically.
        /// </summary>
        public long RemainingActions { get; init; }

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleState FromProto(Api.Schedule.V1.ScheduleState proto) => new()
        {
            Note = string.IsNullOrEmpty(proto.Notes) ? null : proto.Notes,
            Paused = proto.Paused,
            LimitedActions = proto.LimitedActions,
            RemainingActions = proto.RemainingActions,
        };

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <returns>Proto.</returns>
        internal Api.Schedule.V1.ScheduleState ToProto() => new()
        {
            Notes = Note ?? string.Empty,
            Paused = Paused,
            LimitedActions = LimitedActions,
            RemainingActions = RemainingActions,
        };
    }
}