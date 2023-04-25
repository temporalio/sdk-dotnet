namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Action to start a workflow on a listed schedule.
    /// </summary>
    /// <param name="Workflow">Workflow type name.</param>
    public record ScheduleListActionStartWorkflow(string Workflow) : ScheduleListAction
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="workflow">Workflow type name.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleListActionStartWorkflow FromProto(string workflow) => new(workflow);
    }
}