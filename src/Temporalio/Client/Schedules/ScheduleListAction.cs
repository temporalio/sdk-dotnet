using System;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Base class for an action a listed schedule can take. The most common implementation is
    /// <see cref="ScheduleListActionStartWorkflow" />.
    /// </summary>
    public abstract record ScheduleListAction
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleListAction FromProto(Api.Schedule.V1.ScheduleListInfo proto)
        {
            if (proto.WorkflowType == null)
            {
                throw new InvalidOperationException($"Unsupported action on list schedule");
            }
            return ScheduleListActionStartWorkflow.FromProto(proto.WorkflowType.Name);
        }
    }
}