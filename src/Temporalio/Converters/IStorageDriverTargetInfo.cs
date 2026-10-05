#pragma warning disable CA1724 // We don't care that Workflow/Activity clash with API namespace

namespace Temporalio.Converters
{
    /// <summary>
    /// The Temporal execution a payload is being stored on behalf of, made available to
    /// <see cref="IStorageDriver.StoreAsync" /> so drivers can organize storage by execution. The
    /// two implementations used by the SDK are <see cref="Workflow" /> and <see cref="Activity" />.
    /// </summary>
    /// <remarks>
    /// A target is not always known, so drivers must handle a null target and null members.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal interface IStorageDriverTargetInfo
    {
        /// <summary>
        /// Target for payloads stored on behalf of a workflow. Also used for workflow activities,
        /// which store against their owning workflow rather than themselves.
        /// </summary>
        /// <param name="Namespace">Workflow namespace.</param>
        /// <param name="WorkflowId">Workflow ID, if known.</param>
        /// <param name="RunId">Run ID, if known. Unset when the run is not yet determined, e.g. when
        /// starting a child workflow or continuing as new.</param>
        /// <param name="WorkflowType">Workflow type, if known.</param>
        /// <remarks>
        /// WARNING: This constructor may have required properties added. Do not rely on the exact
        /// constructor, only use "with" clauses.
        /// </remarks>
        public sealed record Workflow(
#pragma warning disable CA1716 // We're ok with Namespace identifier here
            string Namespace,
#pragma warning restore CA1716
            string? WorkflowId,
            string? RunId,
            string? WorkflowType) : IStorageDriverTargetInfo;

        /// <summary>
        /// Target for payloads stored on behalf of a standalone activity.
        /// </summary>
        /// <param name="Namespace">Activity namespace.</param>
        /// <param name="ActivityId">Activity ID, if known.</param>
        /// <param name="RunId">Run ID, if known.</param>
        /// <param name="ActivityType">Activity type, if known.</param>
        /// <remarks>
        /// WARNING: This constructor may have required properties added. Do not rely on the exact
        /// constructor, only use "with" clauses.
        /// </remarks>
        public sealed record Activity(
#pragma warning disable CA1716 // We're ok with Namespace identifier here
            string Namespace,
#pragma warning restore CA1716
            string? ActivityId,
            string? RunId,
            string? ActivityType) : IStorageDriverTargetInfo;
    }
}
