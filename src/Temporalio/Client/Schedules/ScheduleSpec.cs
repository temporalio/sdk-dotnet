using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Specification of the times scheduled actions may occur. The times are the union of
    /// <see cref="Calendars" />, <see cref="Intervals" />, and <see cref="CronExpressions" />
    /// excluding anything in <see cref="Skip" />.
    /// </summary>
    public record ScheduleSpec
    {
        /// <summary>
        /// Gets the calendar-based specification of times.
        /// </summary>
        public IReadOnlyCollection<ScheduleCalendarSpec> Calendars { get; init; } = Array.Empty<ScheduleCalendarSpec>();

        /// <summary>
        /// gets the interval-based specification of times.
        /// </summary>
        public IReadOnlyCollection<ScheduleIntervalSpec> Intervals { get; init; } = Array.Empty<ScheduleIntervalSpec>();

        /// <summary>
        /// Gets the cron-based specification of times.
        /// </summary>
        /// <remarks>
        /// This is provided for easy migration from legacy string-based cron scheduling. New uses
        /// should use <see cref="Calendars" /> instead. These expressions will be translated to
        /// calendar-based specifications on the server.
        /// </remarks>
        public IReadOnlyCollection<string> CronExpressions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the set of matching calendar times that will be skipped.
        /// </summary>
        public IReadOnlyCollection<ScheduleCalendarSpec> Skip { get; init; } = Array.Empty<ScheduleCalendarSpec>();

        /// <summary>
        /// Gets the time before which any matching times will be skipped.
        /// </summary>
        public DateTime? StartAt { get; init; }

        /// <summary>
        /// Gets the time after which any matching times will be skipped.
        /// </summary>
        public DateTime? EndAt { get; init; }

        /// <summary>
        /// Gets the jitter to apply to each action.
        /// </summary>
        /// <remarks>
        /// An action's schedule time will be incremented by a random value between 0 and this value
        /// if present (but not past the next schedule).
        /// </remarks>
        public TimeSpan? Jitter { get; init; }

        /// <summary>
        /// Gets the IANA time zone name, for example <c>US/Central</c>.
        /// </summary>
        public string? TimeZoneName { get; init; }

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleSpec FromProto(Api.Schedule.V1.ScheduleSpec proto) => new()
        {
            Calendars = proto.StructuredCalendar.Select(ScheduleCalendarSpec.FromProto).ToList(),
            Intervals = proto.Interval.Select(ScheduleIntervalSpec.FromProto).ToList(),
            CronExpressions = proto.CronString,
            Skip = proto.ExcludeStructuredCalendar.Select(ScheduleCalendarSpec.FromProto).ToList(),
            StartAt = proto.StartTime?.ToDateTime(),
            EndAt = proto.EndTime?.ToDateTime(),
            Jitter = proto.Jitter?.ToTimeSpan(),
            TimeZoneName = string.IsNullOrEmpty(proto.TimezoneName) ? null : proto.TimezoneName,
        };

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <returns>Proto.</returns>
        internal Api.Schedule.V1.ScheduleSpec ToProto() => new()
        {
            StructuredCalendar = { Calendars.Select(c => c.ToProto()) },
            CronString = { CronExpressions },
            Interval = { Intervals.Select(i => i.ToProto()) },
            ExcludeStructuredCalendar = { Skip.Select(s => s.ToProto()) },
            StartTime = StartAt is DateTime startAt ? Timestamp.FromDateTime(startAt) : null,
            EndTime = EndAt is DateTime endAt ? Timestamp.FromDateTime(endAt) : null,
            Jitter = Jitter is TimeSpan jitter ? Duration.FromTimeSpan(jitter) : null,
            TimezoneName = TimeZoneName ?? string.Empty,
        };
    }
}