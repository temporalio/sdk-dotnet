using System;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Policies of a schedule.
    /// </summary>
    public record SchedulePolicy
    {
        /// <summary>
        /// Gets the policy for what happens when an action is started while another is still
        /// running.
        /// </summary>
        public ScheduleOverlapPolicy Overlap { get; init; } = ScheduleOverlapPolicy.Skip;

        /// <summary>
        /// Gets the amount of time in the past to execute misseed actions after a Temporal server
        /// is unavailable.
        /// </summary>
        public TimeSpan CatchupWindow { get; init; } = TimeSpan.FromDays(365);

        /// <summary>
        /// Gets a value indicating whether to pause the schedule if an action fails or times out.
        /// </summary>
        public bool PauseOnFailure { get; init; }

        /// <summary>
        /// Gets a value indicating whether scheduled workflow IDs should be kept as-is without a
        /// timestamp suffix.
        /// </summary>
        public bool KeepOriginalWorkflowId { get; init; }

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static SchedulePolicy FromProto(Api.Schedule.V1.SchedulePolicies proto) => new()
        {
            Overlap = proto.OverlapPolicy,
            CatchupWindow = proto.CatchupWindow.ToTimeSpan(),
            PauseOnFailure = proto.PauseOnFailure,
            KeepOriginalWorkflowId = proto.KeepOriginalWorkflowId,
        };

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <returns>Proto.</returns>
        internal Api.Schedule.V1.SchedulePolicies ToProto() => new()
        {
            OverlapPolicy = Overlap,
            CatchupWindow = Duration.FromTimeSpan(CatchupWindow),
            PauseOnFailure = PauseOnFailure,
            KeepOriginalWorkflowId = KeepOriginalWorkflowId,
        };
    }
}
