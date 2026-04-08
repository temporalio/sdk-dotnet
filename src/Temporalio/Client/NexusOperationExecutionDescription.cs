using System;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Client
{
    /// <summary>
    /// Description of a standalone Nexus operation execution from a describe call.
    /// </summary>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public class NexusOperationExecutionDescription : NexusOperationExecution
    {
        private readonly Lazy<Task<(string? Summary, string? Details)>> userMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="NexusOperationExecutionDescription"/>
        /// class.
        /// </summary>
        /// <param name="rawDescription">Raw proto description.</param>
        /// <param name="clientNamespace">Client namespace.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
        protected internal NexusOperationExecutionDescription(
            DescribeNexusOperationExecutionResponse rawDescription,
            string clientNamespace,
            DataConverter dataConverter)
            : base(
                clientNamespace: clientNamespace,
                operationId: rawDescription.Info.OperationId,
                runId: string.IsNullOrEmpty(rawDescription.Info.RunId) ? null : rawDescription.Info.RunId,
                endpoint: rawDescription.Info.Endpoint,
                service: rawDescription.Info.Service,
                operation: rawDescription.Info.Operation,
                closeTime: rawDescription.Info.CloseTime?.ToDateTime(),
                executionDuration: rawDescription.Info.ExecutionDuration?.ToTimeSpan(),
                scheduledTime: rawDescription.Info.ScheduleTime?.ToDateTime() ?? default,
                stateTransitionCount: rawDescription.Info.StateTransitionCount,
                status: rawDescription.Info.Status,
                searchAttributesFactory: () => rawDescription.Info.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(rawDescription.Info.SearchAttributes))
        {
            RawDescription = rawDescription;
            var info = rawDescription.Info;
            Attempt = info.Attempt;
            BlockedReason = string.IsNullOrEmpty(info.BlockedReason) ? null : info.BlockedReason;
            ExpirationTime = info.ExpirationTime?.ToDateTime();
            LastAttemptCompleteTime = info.LastAttemptCompleteTime?.ToDateTime();
            LongPollToken = rawDescription.LongPollToken.IsEmpty ? null : rawDescription.LongPollToken.ToByteArray();
            NextAttemptScheduleTime = info.NextAttemptScheduleTime?.ToDateTime();
            OperationToken = string.IsNullOrEmpty(info.OperationToken) ? null : info.OperationToken;
            RequestId = string.IsNullOrEmpty(info.RequestId) ? null : info.RequestId;
            ScheduleToCloseTimeout = info.ScheduleToCloseTimeout?.ToTimeSpan();
            State = info.State;
#pragma warning disable VSTHRD011 // This should not be able to deadlock
            userMetadata = new(() => dataConverter.FromUserMetadataAsync(info.UserMetadata));
#pragma warning restore VSTHRD011
        }

        /// <summary>
        /// Gets the current attempt number, starting at 1.
        /// </summary>
        public int Attempt { get; private init; }

        /// <summary>
        /// Gets the blocked reason if the state is blocked.
        /// </summary>
        public string? BlockedReason { get; private init; }

        /// <summary>
        /// Gets the scheduled time plus schedule-to-close timeout.
        /// </summary>
        public DateTime? ExpirationTime { get; private init; }

        /// <summary>
        /// Gets when the last attempt completed.
        /// </summary>
        public DateTime? LastAttemptCompleteTime { get; private init; }

        /// <summary>
        /// Gets the token for follow-on long-poll requests, or null if the operation is complete.
        /// </summary>
        public byte[]? LongPollToken { get; private init; }

        /// <summary>
        /// Gets when the next attempt will be scheduled.
        /// </summary>
        public DateTime? NextAttemptScheduleTime { get; private init; }

        /// <summary>
        /// Gets the operation token for async operations after a successful StartOperation call.
        /// </summary>
        public string? OperationToken { get; private init; }

        /// <summary>
        /// Gets the server-generated request ID used as an idempotency token.
        /// </summary>
        public string? RequestId { get; private init; }

        /// <summary>
        /// Gets the schedule-to-close timeout.
        /// </summary>
        public TimeSpan? ScheduleToCloseTimeout { get; private init; }

        /// <summary>
        /// Gets the more detailed state if the operation status is running.
        /// </summary>
        public PendingNexusOperationState State { get; private init; }

        /// <summary>
        /// Gets the raw proto description.
        /// </summary>
        internal DescribeNexusOperationExecutionResponse RawDescription { get; private init; }

        /// <summary>
        /// Gets the single-line fixed summary for this operation that may appear in UI/CLI.
        /// This can be in single-line Temporal markdown format.
        /// </summary>
        /// <returns>Static summary.</returns>
        public async Task<string?> GetStaticSummaryAsync() =>
            (await userMetadata.Value.ConfigureAwait(false)).Summary;
    }
}
