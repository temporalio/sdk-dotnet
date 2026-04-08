using System;
using System.Threading;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Nexus.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a standalone Nexus operation execution from a list call.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationExecution
    {
        private readonly Lazy<SearchAttributeCollection> searchAttributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationExecution"/> class from list
        /// info.
        /// </summary>
        /// <param name="rawInfo">Raw proto list info.</param>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
        protected internal NexusOperationExecution(
            NexusOperationExecutionListInfo rawInfo, string clientNamespace)
            : this(
                clientNamespace: clientNamespace,
                operationId: rawInfo.OperationId,
                runId: string.IsNullOrEmpty(rawInfo.RunId) ? null : rawInfo.RunId,
                endpoint: rawInfo.Endpoint,
                service: rawInfo.Service,
                operation: rawInfo.Operation,
                closeTime: rawInfo.CloseTime?.ToDateTime(),
                executionDuration: rawInfo.ExecutionDuration?.ToTimeSpan(),
                scheduledTime: rawInfo.ScheduleTime?.ToDateTime() ?? default,
                stateTransitionCount: rawInfo.StateTransitionCount,
                status: rawInfo.Status,
                searchAttributesFactory: () => rawInfo.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(rawInfo.SearchAttributes))
        {
            RawInfo = rawInfo;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationExecution"/> class.
        /// </summary>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="operationId">Operation ID.</param>
        /// <param name="runId">Operation run ID.</param>
        /// <param name="endpoint">Endpoint name.</param>
        /// <param name="service">Service name.</param>
        /// <param name="operation">Operation name.</param>
        /// <param name="closeTime">Close time.</param>
        /// <param name="executionDuration">Execution duration.</param>
        /// <param name="scheduledTime">Scheduled time.</param>
        /// <param name="stateTransitionCount">State transition count.</param>
        /// <param name="status">Operation status.</param>
        /// <param name="searchAttributesFactory">Factory for lazy search attribute creation.</param>
        private protected NexusOperationExecution(
            string clientNamespace,
            string operationId,
            string? runId,
            string endpoint,
            string service,
            string operation,
            DateTime? closeTime,
            TimeSpan? executionDuration,
            DateTime scheduledTime,
            long stateTransitionCount,
            NexusOperationExecutionStatus status,
            Func<SearchAttributeCollection> searchAttributesFactory)
        {
            Namespace = clientNamespace;
            OperationId = operationId;
            RunId = runId;
            Endpoint = endpoint;
            Service = service;
            Operation = operation;
            CloseTime = closeTime;
            ExecutionDuration = executionDuration;
            ScheduledTime = scheduledTime;
            StateTransitionCount = stateTransitionCount;
            Status = status;
            searchAttributes = new(searchAttributesFactory, LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Gets the operation ID.
        /// </summary>
        public string OperationId { get; private init; }

        /// <summary>
        /// Gets the operation run ID.
        /// </summary>
        public string? RunId { get; private init; }

        /// <summary>
        /// Gets the endpoint name.
        /// </summary>
        public string Endpoint { get; private init; }

        /// <summary>
        /// Gets the service name.
        /// </summary>
        public string Service { get; private init; }

        /// <summary>
        /// Gets the operation name.
        /// </summary>
        public string Operation { get; private init; }

        /// <summary>
        /// Gets when the operation was closed if in a terminal state.
        /// </summary>
        public DateTime? CloseTime { get; private init; }

        /// <summary>
        /// Gets the total execution duration if the operation is closed.
        /// </summary>
        public TimeSpan? ExecutionDuration { get; private init; }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace { get; private init; }

        /// <summary>
        /// Gets when the operation was originally scheduled.
        /// </summary>
        public DateTime ScheduledTime { get; private init; }

        /// <summary>
        /// Gets the number of state transitions.
        /// </summary>
        public long StateTransitionCount { get; private init; }

        /// <summary>
        /// Gets the status of the operation.
        /// </summary>
        public NexusOperationExecutionStatus Status { get; private init; }

        /// <summary>
        /// Gets the search attributes on the operation.
        /// </summary>
        /// <remarks>This is lazily converted on first access.</remarks>
        public SearchAttributeCollection TypedSearchAttributes => searchAttributes.Value;

        /// <summary>
        /// Gets the raw proto list info, or null if this was created from a describe call.
        /// </summary>
        internal NexusOperationExecutionListInfo? RawInfo { get; private init; }
    }
}
