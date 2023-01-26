using System;
using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Workflow.V1;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a workflow execution.
    /// </summary>
    /// <param name="RawInfo">Underlying protobuf information.</param>
    public record WorkflowExecution(WorkflowExecutionInfo RawInfo)
    {
        /// <summary>
        /// Gets when the workflow was closed if closed.
        /// </summary>
        public DateTime? CloseTime => RawInfo.CloseTime?.ToDateTime();

        /// <summary>
        /// Gets when the workflow run started or should start.
        /// </summary>
        public DateTime? ExecutionTime => RawInfo.ExecutionTime?.ToDateTime();

        /// <summary>
        /// Gets the number of events in history.
        /// </summary>
        public int HistoryLength => (int)RawInfo.HistoryLength;

        /// <summary>
        /// Gets the ID for the workflow.
        /// </summary>
        public string ID => RawInfo.Execution.WorkflowId;

        /// <summary>
        /// Gets the workflow memo dictionary if present.
        /// </summary>
        public IDictionary<string, Payload>? Memo => RawInfo.Memo?.Fields;

        /// <summary>
        /// Gets the ID for the parent workflow if this was started as a child.
        /// </summary>
        public string? ParentID => RawInfo.ParentExecution?.WorkflowId;

        /// <summary>
        /// Gets the run ID for the parent workflow if this was started as a child.
        /// </summary>
        public string? ParentRunID => RawInfo.ParentExecution?.RunId;

        /// <summary>
        /// Gets the run ID for the workflow.
        /// </summary>
        public string RunID => RawInfo.Execution.RunId;

        /// <summary>
        /// Gets the workflow search attribute dictionary if present.
        /// </summary>
        public IDictionary<string, Payload>? SearchAttributes =>
            RawInfo.SearchAttributes?.IndexedFields;

        /// <summary>
        /// Gets when the workflow was created.
        /// </summary>
        public DateTime StartTime => RawInfo.StartTime.ToDateTime();

        /// <summary>
        /// Gets the status for the workflow.
        /// </summary>
        public WorkflowExecutionStatus Status => RawInfo.Status;

        /// <summary>
        /// Gets the task queue for the workflow.
        /// </summary>
        public string TaskQueue => RawInfo.TaskQueue;

        /// <summary>
        /// Gets the type name of the workflow.
        /// </summary>
        public string WorkflowType => RawInfo.Type.Name;
    }
}