using System;
using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Information about the running workflow.
    /// </summary>
    /// <param name="Attempt">Current workflow attempt.</param>
    /// <param name="ContinuedRunID">Run ID if this was continued.</param>
    /// <param name="CronSchedule">Cron schedule if applicable.</param>
    /// <param name="ExecutionTimeout">Execution timeout for the workflow.</param>
    /// <param name="Namespace">Namespace for the workflow.</param>
    /// <param name="Parent">Parent information for the workflow if this is a child.</param>
    /// <param name="RawMemo">
    /// Raw memo values. Note: This may be mutated internally during workflow execution. Do not
    /// mutate.
    /// </param>
    /// <param name="RawSearchAttributes">
    /// Raw search attribute values. Note: This may be mutated internally during workflow
    /// execution. Do not mutate.
    /// </param>
    /// <param name="RetryPolicy">Retry policy for the workflow.</param>
    /// <param name="RunID">Run ID for the workflow.</param>
    /// <param name="RunTimeout">Run timeout for the workflow.</param>
    /// <param name="StartTime">Time when the workflow started.</param>
    /// <param name="TaskQueue">Task queue for the workflow.</param>
    /// <param name="TaskTimeout">Task timeout for the workflow.</param>
    /// <param name="WorkflowID">ID for the workflow.</param>
    /// <param name="WorkflowType">Workflow type name.</param>
    public record WorkflowInfo(
        int Attempt,
        string? ContinuedRunID,
        string? CronSchedule,
        TimeSpan? ExecutionTimeout,
        string Namespace,
        WorkflowInfo.ParentInfo? Parent,
        Memo? RawMemo,
        SearchAttributes? RawSearchAttributes,
        RetryPolicy? RetryPolicy,
        string RunID,
        TimeSpan? RunTimeout,
        DateTime StartTime,
        string TaskQueue,
        TimeSpan TaskTimeout,
        string WorkflowID,
        string WorkflowType)
    {
        /// <summary>
        /// Gets the value that is set on
        /// <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope" /> before this activity is
        /// started.
        /// </summary>
        internal Dictionary<string, object> LoggerScope { get; } = new()
        {
            ["Attempt"] = Attempt,
            ["Namespace"] = Namespace,
            ["RunID"] = RunID,
            ["TaskQueue"] = TaskQueue,
            ["WorkflowID"] = WorkflowID,
            ["WorkflowType"] = WorkflowType,
        };

        /// <summary>
        /// Information about a parent of a workflow.
        /// </summary>
        /// <param name="Namespace">Namespace for the parent.</param>
        /// <param name="RunID">Run ID for the parent.</param>
        /// <param name="WorkflowID">Workflow ID for the parent.</param>
        public record ParentInfo(
            string Namespace,
            string RunID,
            string WorkflowID);
    }
}