using System;
using System.Collections.Generic;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Specification relative to calendar time when to run an action.
    /// </summary>
    /// <remarks>
    /// A timestamp matches if at least one range of each field matches except for year. If year is
    /// missing, that means all years match. For all fields besides year, at least one range must be
    /// present to match anything.
    /// </remarks>
    public record ScheduleCalendarSpec
    {
        /// <summary>
        /// Default range set for zero.
        /// </summary>
        public static readonly IReadOnlyCollection<ScheduleRange> Beginning = new[] { ScheduleRange.Zero };

        /// <summary>
        /// Default range set for all days in a month.
        /// </summary>
        public static readonly IReadOnlyCollection<ScheduleRange> AllMonthDays = new[] { new ScheduleRange(1, 31) };

        /// <summary>
        /// Default range set for all months in a year.
        /// </summary>
        public static readonly IReadOnlyCollection<ScheduleRange> AllMonths = new[] { new ScheduleRange(1, 12) };

        /// <summary>
        /// Default range set for all days in a week.
        /// </summary>
        public static readonly IReadOnlyCollection<ScheduleRange> AllWeekDays = new[] { new ScheduleRange(0, 6) };

        /// <summary>
        /// Gets the second range to match, 0-59. Default matches 0.
        /// </summary>
        public IReadOnlyCollection<ScheduleRange> Second { get; init; } = Beginning;

        /// <summary>
        /// Gets the minute range to match, 0-59. Default matches 0.
        /// </summary>
        public IReadOnlyCollection<ScheduleRange> Minute { get; init; } = Beginning;

        /// <summary>
        /// Gets the hour range to match, 0-23. Default matches 0.
        /// </summary>
        public IReadOnlyCollection<ScheduleRange> Hour { get; init; } = Beginning;

        /// <summary>
        /// Gets the day of month range to match, 1-31. Default matches all days.
        /// </summary>
        public IReadOnlyCollection<ScheduleRange> DayOfMonth { get; init; } = AllMonthDays;

        /// <summary>
        /// Gets the month range to match, 1-12. Default matches all months.
        /// </summary>
        public IReadOnlyCollection<ScheduleRange> Month { get; init; } = AllMonths;

        /// <summary>
        /// Gets the optional year to match. Default of empty matches all years.
        /// </summary>
        public IReadOnlyCollection<ScheduleRange> Year { get; init; } = Array.Empty<ScheduleRange>();

        /// <summary>
        /// Gets the day of week range to match, 0-6, 0 is Sunday. Default matches all days.
        /// </summary>
        public IReadOnlyCollection<ScheduleRange> DayOfWeek { get; init; } = AllWeekDays;

        /// <summary>
        /// Gets the description of this specification.
        /// </summary>
        public string? Comment { get; init; }

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleCalendarSpec FromProto(Api.Schedule.V1.StructuredCalendarSpec proto) =>
            new()
            {
                Second = ScheduleRange.FromProtos(proto.Second),
                Minute = ScheduleRange.FromProtos(proto.Minute),
                Hour = ScheduleRange.FromProtos(proto.Hour),
                DayOfMonth = ScheduleRange.FromProtos(proto.DayOfMonth),
                Month = ScheduleRange.FromProtos(proto.Month),
                Year = ScheduleRange.FromProtos(proto.Year),
                DayOfWeek = ScheduleRange.FromProtos(proto.DayOfWeek),
                Comment = string.IsNullOrEmpty(proto.Comment) ? null : proto.Comment,
            };

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <returns>Proto.</returns>
        internal Api.Schedule.V1.StructuredCalendarSpec ToProto() => new()
        {
            Second = { ScheduleRange.ToProtos(Second) },
            Minute = { ScheduleRange.ToProtos(Minute) },
            Hour = { ScheduleRange.ToProtos(Hour) },
            DayOfMonth = { ScheduleRange.ToProtos(DayOfMonth) },
            Month = { ScheduleRange.ToProtos(Month) },
            Year = { ScheduleRange.ToProtos(Year) },
            DayOfWeek = { ScheduleRange.ToProtos(DayOfWeek) },
            Comment = Comment ?? string.Empty,
        };
    }
}