using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Inclusive range for a schedule match value.
    /// </summary>
    /// <param name="Start">Inclusive start of the range.</param>
    /// <param name="End">Inclusive end of the range. If unset or less than start, defaults to
    /// start.</param>
    /// <param name="Step">Step to take between each value. Unset or 0 defaults as 1.</param>
    public record ScheduleRange(
        int Start,
        int End = 0,
        int Step = 0)
    {
        /// <summary>
        /// Zero range.
        /// </summary>
        public static readonly ScheduleRange Zero = new(0);

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleRange FromProto(Api.Schedule.V1.Range proto) => new(
            Start: proto.Start,
            End: proto.End,
            Step: proto.Step);

        /// <summary>
        /// Convert from protos.
        /// </summary>
        /// <param name="protos">Protos.</param>
        /// <returns>Converted value.</returns>
        internal static List<ScheduleRange> FromProtos(
            IEnumerable<Api.Schedule.V1.Range> protos) => protos.Select(FromProto).ToList();

        /// <summary>
        /// Convert to protos.
        /// </summary>
        /// <param name="ranges">Ranges to convert.</param>
        /// <returns>Protos.</returns>
        internal static IEnumerable<Api.Schedule.V1.Range> ToProtos(
            IEnumerable<ScheduleRange> ranges) => ranges.Select(r => r.ToProto());

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <returns>Proto.</returns>
        internal Api.Schedule.V1.Range ToProto() => new()
        {
            Start = Start,
            End = End,
            Step = Step,
        };
    }
}