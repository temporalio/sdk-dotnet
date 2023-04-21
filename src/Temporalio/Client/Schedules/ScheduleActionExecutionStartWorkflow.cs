namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Action execution representing a scheduled workflow start.
    /// </summary>
    /// <param name="WorkflowID">Workflow ID.</param>
    /// <param name="FirstExecutionRunID">Run ID.</param>
    public record ScheduleActionExecutionStartWorkflow(
        string WorkflowID,
        string FirstExecutionRunID) : ScheduleActionExecution
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleActionExecutionStartWorkflow FromProto(
            Api.Common.V1.WorkflowExecution proto) =>
            new(WorkflowID: proto.WorkflowId, FirstExecutionRunID: proto.RunId);
    }
}