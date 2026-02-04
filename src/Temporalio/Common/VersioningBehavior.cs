namespace Temporalio.Common
{
    /// <summary>
    /// Specifies when a workflow might move from a worker of one Build ID to another.
    /// </summary>
    public enum VersioningBehavior
    {
        /// <summary>
        /// An unspecified versioning behavior. By default, workers opting into worker versioning will
        /// be required to specify a behavior. See <see cref="Temporalio.Worker.WorkerDeploymentOptions"/>.
        /// </summary>
        Unspecified = Temporalio.Api.Enums.V1.VersioningBehavior.Unspecified,

        /// <summary>
        /// The workflow will be pinned to the current Build ID unless manually moved.
        /// </summary>
        Pinned = Temporalio.Api.Enums.V1.VersioningBehavior.Pinned,

        /// <summary>
        /// The workflow will automatically move to the latest version (default Build ID of the task
        /// queue) when the next task is dispatched.
        /// </summary>
        AutoUpgrade = Temporalio.Api.Enums.V1.VersioningBehavior.AutoUpgrade,
    }
}
