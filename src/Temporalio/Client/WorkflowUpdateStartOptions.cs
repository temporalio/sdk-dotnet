#pragma warning disable SA1402 // We allow multiple types of the same name

namespace Temporalio.Client
{
    /// <summary>
    /// Options for starting an update on a <see cref="WorkflowHandle" />.
    /// </summary>
    public class WorkflowUpdateStartOptions : WorkflowUpdateOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateStartOptions"/> class.
        /// </summary>
        public WorkflowUpdateStartOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateStartOptions"/> class.
        /// </summary>
        /// <param name="waitForStage">Stage to wait for.</param>
        public WorkflowUpdateStartOptions(WorkflowUpdateStage waitForStage) =>
            WaitForStage = waitForStage;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowUpdateStartOptions"/> class.
        /// </summary>
        /// <param name="id">Update ID.</param>
        /// <param name="waitForStage">Stage to wait for.</param>
        public WorkflowUpdateStartOptions(string id, WorkflowUpdateStage waitForStage)
            : base(id) => WaitForStage = waitForStage;

        /// <summary>
        /// Gets or sets the stage to wait for on start. This is required and cannot be set to
        /// <c>None</c> or <c>Admitted</c> at this time.
        /// </summary>
        public WorkflowUpdateStage WaitForStage { get; set; }

        /// <summary>
        /// Gets or sets the request ID for server de-duplication. Only settable by the SDK, e.g.
        /// when starting an update-workflow-backed Nexus operation.
        /// </summary>
        internal string? RequestId { get; set; }

        /// <summary>
        /// Gets or sets the completion callbacks. Only settable by the SDK, e.g. when starting an
        /// update-workflow-backed Nexus operation.
        /// </summary>
        internal System.Collections.Generic.IReadOnlyCollection<Api.Common.V1.Callback>? CompletionCallbacks { get; set; }

        /// <summary>
        /// Gets or sets the links. Only settable by the SDK, e.g. when starting an
        /// update-workflow-backed Nexus operation.
        /// </summary>
        internal System.Collections.Generic.IReadOnlyCollection<Api.Common.V1.Link>? Links { get; set; }

        /// <summary>
        /// Gets or sets a holder that captures the link returned on the start-update response. Only
        /// set by the SDK, e.g. when starting an update-workflow-backed Nexus operation.
        /// </summary>
        internal UpdateResponseInfo? ResponseInfo { get; set; }
    }

    /// <summary>
    /// Mutable holder used to capture the link returned on a start-update response, so callers such
    /// as the Nexus update-workflow operation can build outbound links.
    /// </summary>
    internal sealed class UpdateResponseInfo
    {
        /// <summary>
        /// Gets or sets the link returned on the start-update response, if any.
        /// </summary>
        internal Api.Common.V1.Link? Link { get; set; }
    }
}