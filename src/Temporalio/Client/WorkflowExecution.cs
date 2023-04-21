using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Workflow.V1;
using Temporalio.Converters;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a workflow execution.
    /// </summary>
    public class WorkflowExecution
    {
        private readonly Lazy<IReadOnlyDictionary<string, IEncodedRawValue>> memo;
        private readonly Lazy<SearchAttributeCollection> searchAttributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowExecution"/> class.
        /// </summary>
        /// <param name="rawInfo">Raw proto info.</param>
        /// <param name="dataConverter">Data converter used for memos.</param>
        internal WorkflowExecution(WorkflowExecutionInfo rawInfo, DataConverter dataConverter)
        {
            RawInfo = rawInfo;
            // Search attribute conversion is cheap so it doesn't need to lock on publication. But
            // memo conversion may use remote codec so it should only ever be created once lazily.
            memo = new(
                () => rawInfo.Memo == null ? new Dictionary<string, IEncodedRawValue>(0) :
                    rawInfo.Memo.Fields.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (IEncodedRawValue)new EncodedRawValue(dataConverter, kvp.Value)));
            searchAttributes = new(
                () => rawInfo.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(rawInfo.SearchAttributes),
                LazyThreadSafetyMode.PublicationOnly);
        }

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
        /// Gets the workflow memo dictionary, lazily creating when accessed.
        /// </summary>
        public IReadOnlyDictionary<string, IEncodedRawValue> Memo => memo.Value;

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
        /// Gets the search attributes on the workflow.
        /// </summary>
        /// <remarks>
        /// This is lazily converted on first access.
        /// </remarks>
        public SearchAttributeCollection TypedSearchAttributes => searchAttributes.Value;

        /// <summary>
        /// Gets the type name of the workflow.
        /// </summary>
        public string WorkflowType => RawInfo.Type.Name;

        /// <summary>
        /// Gets the raw proto info.
        /// </summary>
        internal WorkflowExecutionInfo RawInfo { get; private init; }
    }
}