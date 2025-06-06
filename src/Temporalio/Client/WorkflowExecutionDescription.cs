using System;
using System.Threading.Tasks;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Converters;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a workflow execution and description.
    /// </summary>
    public class WorkflowExecutionDescription : WorkflowExecution
    {
        private Lazy<Task<(string? Summary, string? Details)>> userMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowExecutionDescription"/> class.
        /// </summary>
        /// <param name="rawDescription">Raw protobuf description.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
        protected internal WorkflowExecutionDescription(
            DescribeWorkflowExecutionResponse rawDescription, DataConverter dataConverter)
            : base(rawDescription.WorkflowExecutionInfo, dataConverter, null)
        {
            RawDescription = rawDescription;
#pragma warning disable VSTHRD011 // This should not be able to deadlock
            userMetadata = new(() => dataConverter.FromUserMetadataAsync(
                rawDescription.ExecutionConfig?.UserMetadata));
#pragma warning restore VSTHRD011
        }

        /// <summary>
        /// Gets the raw proto info.
        /// </summary>
        internal DescribeWorkflowExecutionResponse RawDescription { get; private init; }

        /// <summary>
        /// Gets the single-line fixed summary for this workflow execution that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        /// <returns>Static summary.</returns>
        public async Task<string?> GetStaticSummaryAsync() =>
            (await userMetadata.Value.ConfigureAwait(false)).Summary;

        /// <summary>
        /// Gets the general fixed details for this workflow execution that may appear in UI/CLI.
        /// This can be in Temporal markdown format and can span multiple lines.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        /// <returns>Static details.</returns>
        public async Task<string?> GetStaticDetailsAsync() =>
            (await userMetadata.Value.ConfigureAwait(false)).Details;
    }
}