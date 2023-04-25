using System;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Time period and policy for actions taken as if the time passed right now.
    /// </summary>
    /// <param name="StartAt">Start of the range to evaluate the schedule in. This is
    /// exclusive.</param>
    /// <param name="EndAt">End of the range to evaluate the schedule in. This is inclusive.</param>
    /// <param name="Overlap">Overlap policy.</param>
    public record ScheduleBackfill(
        DateTime StartAt,
        DateTime EndAt,
        ScheduleOverlapPolicy Overlap = ScheduleOverlapPolicy.Unspecified)
    {
        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <returns>Proto.</returns>
        internal Api.Schedule.V1.BackfillRequest ToProto() => new()
        {
            StartTime = Timestamp.FromDateTime(StartAt),
            EndTime = Timestamp.FromDateTime(EndAt),
            OverlapPolicy = Overlap,
        };
    }
}