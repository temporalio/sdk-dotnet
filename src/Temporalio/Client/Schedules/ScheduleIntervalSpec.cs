using System;
using Google.Protobuf.WellKnownTypes;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Specification for scheduling on an interval. Matching times are expressed as
    /// <c>epoch + (n * every) + offset</c>.
    /// </summary>
    /// <param name="Every">Period to repeat the interval.</param>
    /// <param name="Offset">Fixed offset added to each interval period.</param>
    public record ScheduleIntervalSpec(
        TimeSpan Every,
        TimeSpan? Offset = null)
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleIntervalSpec FromProto(Api.Schedule.V1.IntervalSpec proto) => new(
            Every: proto.Interval.ToTimeSpan(),
            Offset: proto.Phase?.ToTimeSpan());

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <returns>Proto.</returns>
        internal Api.Schedule.V1.IntervalSpec ToProto() => new()
        {
            Interval = Duration.FromTimeSpan(Every),
            Phase = Offset is TimeSpan offset ? Duration.FromTimeSpan(offset) : null,
        };
    }
}