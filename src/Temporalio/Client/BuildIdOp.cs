using System;

namespace Temporalio.Client
{
    /// <summary>
    /// A supertype for all operations that may be performed by <see cref="ITemporalClient.UpdateWorkerBuildIdCompatibilityAsync"/>.
    /// </summary>
    [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
    public abstract record BuildIdOp
    {
        private BuildIdOp()
        {
        }

        /// <summary>
        /// Adds a new Build ID into a new set, which will be used as the default set for the queue. This means all new
        /// workflows will start on this Build ID.
        /// </summary>
        /// <param name="BuildId">The Build ID to add.</param>
        public record AddNewDefault(string BuildId) : BuildIdOp;

        /// <summary>
        /// Adds a new Build ID into an existing compatible set. The newly added ID becomes the default for that
        /// compatible set, and thus new workflow tasks for workflows which have been executing on workers in that set
        /// will now start on this new Build ID.
        /// </summary>
        /// <param name="BuildId">The new Build ID to add.</param>
        /// <param name="ExistingCompatibleBuildId">A Build ID which must already be defined on the task queue, and is
        /// used to find the compatible set to add the new id to.</param>
        /// <param name="MakeSetDefault">If set to true, the targeted set will also be promoted to become the overall
        /// default set for the queue.</param>
        public record AddNewCompatible(
            string BuildId, string ExistingCompatibleBuildId, bool MakeSetDefault = false) : BuildIdOp;

        /// <summary>
        /// Promotes a set of compatible Build IDs to become the current default set for the task queue. Any Build ID
        /// in the set may be used to target it.
        /// </summary>
        /// <param name="BuildId">A Build ID which must already be defined on the task queue, and is used to find the
        /// compatible set to promote.</param>
        public record PromoteSetByBuildId(string BuildId) : BuildIdOp;

        /// <summary>
        /// Promotes a Build ID within an existing set to become the default ID for that set.
        /// </summary>
        /// <param name="BuildId">The Build ID to promote.</param>
        public record PromoteBuildIdWithinSet(string BuildId) : BuildIdOp;

        /// <summary>
        /// Merges two sets into one set, thus declaring all the Build IDs in both as compatible with one another. The
        /// default of the primary set is maintained as the merged set's overall default.
        /// </summary>
        /// <param name="PrimaryBuildId">A Build ID which and is used to find the primary set to be merged.</param>
        /// <param name="SecondaryBuildId">A Build ID which and is used to find the secondary set to be merged.</param>
        public record MergeSets(string PrimaryBuildId, string SecondaryBuildId) : BuildIdOp;
    }
}