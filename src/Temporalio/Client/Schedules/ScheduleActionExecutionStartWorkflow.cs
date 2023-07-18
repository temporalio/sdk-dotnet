namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Action execution representing a scheduled workflow start.
    /// </summary>
    /// <param name="WorkflowId">Workflow ID.</param>
    /// <param name="FirstExecutionRunId">Run ID.</param>
    public record ScheduleActionExecutionStartWorkflow(
        string WorkflowId,
        string FirstExecutionRunId) : ScheduleActionExecution
    {
        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value.</returns>
        internal static ScheduleActionExecutionStartWorkflow FromProto(
            Api.Common.V1.WorkflowExecution proto) =>
            new(WorkflowId: proto.WorkflowId, FirstExecutionRunId: proto.RunId);
    }
}