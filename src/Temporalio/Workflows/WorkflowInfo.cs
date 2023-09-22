using System;
using System.Collections.Generic;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Information about the running workflow. This is immutable for the life of the workflow run.
    /// </summary>
    /// <param name="Attempt">Current workflow attempt.</param>
    /// <param name="ContinuedRunId">Run ID if this was continued.</param>
    /// <param name="CronSchedule">Cron schedule if applicable.</param>
    /// <param name="ExecutionTimeout">Execution timeout for the workflow.</param>
    /// <param name="Headers">Headers from when the workflow was started.</param>
    /// <param name="LastFailure">Failure if this workflow run is a continuation of a failure.</param>
    /// <param name="LastResult">Successful result if this workflow is a continuation of a success.</param>
    /// <param name="Namespace">Namespace for the workflow.</param>
    /// <param name="Parent">Parent information for the workflow if this is a child.</param>
    /// <param name="RetryPolicy">Retry policy for the workflow.</param>
    /// <param name="RunId">Run ID for the workflow.</param>
    /// <param name="RunTimeout">Run timeout for the workflow.</param>
    /// <param name="StartTime">Time when the workflow started.</param>
    /// <param name="TaskQueue">Task queue for the workflow.</param>
    /// <param name="TaskTimeout">Task timeout for the workflow.</param>
    /// <param name="WorkflowId">ID for the workflow.</param>
    /// <param name="WorkflowType">Workflow type name.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record WorkflowInfo(
        int Attempt,
        string? ContinuedRunId,
        string? CronSchedule,
        TimeSpan? ExecutionTimeout,
        IReadOnlyDictionary<string, Api.Common.V1.Payload>? Headers,
        Exception? LastFailure,
        IReadOnlyCollection<IRawValue>? LastResult,
        string Namespace,
        WorkflowInfo.ParentInfo? Parent,
        RetryPolicy? RetryPolicy,
        string RunId,
        TimeSpan? RunTimeout,
        DateTime StartTime,
        string TaskQueue,
        TimeSpan TaskTimeout,
        string WorkflowId,
        string WorkflowType)
    {
        /// <summary>
        /// Gets the value that is set on
        /// <see cref="Microsoft.Extensions.Logging.ILogger.BeginScope" /> before this workflow is
        /// started.
        /// </summary>
        internal Dictionary<string, object> LoggerScope { get; } = new()
        {
            ["Attempt"] = Attempt,
            ["Namespace"] = Namespace,
            ["RunId"] = RunId,
            ["TaskQueue"] = TaskQueue,
            ["WorkflowId"] = WorkflowId,
            ["WorkflowType"] = WorkflowType,
        };

        /// <summary>
        /// Information about a parent of a workflow.
        /// </summary>
        /// <param name="Namespace">Namespace for the parent.</param>
        /// <param name="RunId">Run ID for the parent.</param>
        /// <param name="WorkflowId">Workflow ID for the parent.</param>
        public record ParentInfo(
            string Namespace,
            string RunId,
            string WorkflowId);
    }
}