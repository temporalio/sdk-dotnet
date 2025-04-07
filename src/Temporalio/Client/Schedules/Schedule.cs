using System.Threading.Tasks;
using Temporalio.Converters;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// A schedule for periodically running an action.
    /// </summary>
    /// <param name="Action">Action taken when scheduled.</param>
    /// <param name="Spec">When the action is taken.</param>
#pragma warning disable CA1724 // We don't care that this clashes with an API package
    public record Schedule(
#pragma warning restore CA1724
        ScheduleAction Action,
        ScheduleSpec Spec)
    {
        /// <summary>
        /// Gets the policy for the schedule.
        /// </summary>
        public SchedulePolicy Policy { get; init; } = new();

        /// <summary>
        /// Gets the state of the schedule.
        /// </summary>
        public ScheduleState State { get; init; } = new();

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <returns>Converted value.</returns>
        internal static async Task<Schedule> FromProtoAsync(
            Api.Schedule.V1.Schedule proto, string clientNamespace, DataConverter dataConverter) =>
            new(
                Action: await ScheduleAction.FromProtoAsync(proto.Action, clientNamespace, dataConverter).ConfigureAwait(false),
                Spec: ScheduleSpec.FromProto(proto.Spec))
            {
                Policy = SchedulePolicy.FromProto(proto.Policies),
                State = ScheduleState.FromProto(proto.State),
            };

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <returns>Proto.</returns>
        internal async Task<Api.Schedule.V1.Schedule> ToProtoAsync(
            string clientNamespace, DataConverter dataConverter) => new()
        {
            Spec = Spec.ToProto(),
            Action = await Action.ToProtoAsync(clientNamespace, dataConverter).ConfigureAwait(false),
            Policies = Policy.ToProto(),
            State = State.ToProto(),
        };
    }
}