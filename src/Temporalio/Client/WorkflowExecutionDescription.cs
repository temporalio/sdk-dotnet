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
        private WorkflowExecutionDescription(
            DescribeWorkflowExecutionResponse rawDescription,
            string? staticSummary,
            string? staticDetails,
            DataConverter dataConverter)
            : base(rawDescription.WorkflowExecutionInfo, dataConverter)
        {
            RawDescription = rawDescription;
            StaticSummary = staticSummary;
            StaticDetails = staticDetails;
        }

        /// <summary>
        /// Gets the single-line fixed summary for this workflow execution that may appear in
        /// UI/CLI. This can be in single-line Temporal markdown format.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticSummary { get; private init; }

        /// <summary>
        /// Gets the general fixed details for this workflow execution that may appear in UI/CLI.
        /// This can be in Temporal markdown format and can span multiple lines.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? StaticDetails { get; private init; }

        /// <summary>
        /// Gets the raw proto info.
        /// </summary>
        internal DescribeWorkflowExecutionResponse RawDescription { get; private init; }

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="rawDescription">Raw description.</param>
        /// <param name="dataConverter">Data converter.</param>
        /// <returns>Converted value.</returns>
        internal static async Task<WorkflowExecutionDescription> FromProtoAsync(
            DescribeWorkflowExecutionResponse rawDescription, DataConverter dataConverter)
        {
            var (staticSummary, staticDetails) = await dataConverter.FromUserMetadataAsync(
                rawDescription.ExecutionConfig?.UserMetadata).ConfigureAwait(false);
            return new(rawDescription, staticSummary, staticDetails, dataConverter);
        }
    }
}