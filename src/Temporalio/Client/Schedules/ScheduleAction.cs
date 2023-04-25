using System;
using System.Threading.Tasks;
using Temporalio.Converters;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Base class for an action a schedule can take. See <see cref="ScheduleActionStartWorkflow" />
    /// for the most commonly used implementation.
    /// </summary>
    public abstract record ScheduleAction
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleAction FromProto(
            Api.Schedule.V1.ScheduleAction proto, DataConverter dataConverter)
        {
            if (proto.StartWorkflow != null)
            {
                return ScheduleActionStartWorkflow.FromProto(proto.StartWorkflow, dataConverter);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported action {proto.ActionCase}");
            }
        }

        /// <summary>
        /// Convert to proto.
        /// </summary>
        /// <param name="dataConverter">Data converter.</param>
        /// <returns>Proto.</returns>
        internal abstract Task<Api.Schedule.V1.ScheduleAction> ToProtoAsync(DataConverter dataConverter);
    }
}