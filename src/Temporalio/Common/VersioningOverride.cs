using System;

namespace Temporalio.Common
{
    /// <summary>
    /// Represents the override of a worker’s versioning behavior for a workflow execution.
    /// Exactly one of the subtypes must be used.
    /// </summary>
    public abstract record VersioningOverride
    {
        private VersioningOverride()
        {
        }

        /// <summary>
        /// Specifies different sub‐types of pinned override.
        /// </summary>
        public enum PinnedOverrideBehavior
        {
            /// <summary>Unspecified.</summary>
            Unspecified = Temporalio.Api.Workflow.V1.VersioningOverride.Types.PinnedOverrideBehavior.Unspecified,

            /// <summary>Override workflow behavior to be pinned.</summary>
            Pinned = Temporalio.Api.Workflow.V1.VersioningOverride.Types.PinnedOverrideBehavior.Pinned,
        }

        /// <summary>
        /// Converts this override to a protobuf message.
        /// </summary>
        /// <returns>The proto representation.</returns>
        internal Temporalio.Api.Workflow.V1.VersioningOverride ToProto()
        {
            // TODO: Remove deprecated field setting after they've been removed from server
            if (this is PinnedVersioningOverride pv)
            {
                Console.WriteLine("Converting pinned versioning override to proto");
                return new()
                {
#pragma warning disable CS0612
                    Behavior = Temporalio.Api.Enums.V1.VersioningBehavior.Pinned,
                    PinnedVersion = pv.Version.ToCanonicalString(),
#pragma warning restore CS0612
                    Pinned =
                         new Temporalio.Api.Workflow.V1.VersioningOverride.Types.PinnedOverride
                         {
                             Version = pv.Version.ToProto(),
                             Behavior = (Api.Workflow.V1.VersioningOverride.Types.PinnedOverrideBehavior)pv.Behavior,
                         },
                };
            }
            else
            {
                return new()
                {
#pragma warning disable CS0612
                    Behavior = Temporalio.Api.Enums.V1.VersioningBehavior.AutoUpgrade,
#pragma warning restore CS0612
                    AutoUpgrade = true,
                };
            }
        }

        /// <summary>
        /// Workflow will be pinned to a specific deployment version.
        /// </summary>
        /// <param name="Version">The worker deployment version to pin the workflow to.</param>
        /// <param name="Behavior">The behavior of the pinned override.</param>
        public sealed record PinnedVersioningOverride(
            WorkerDeploymentVersion Version,
            PinnedOverrideBehavior Behavior = PinnedOverrideBehavior.Pinned) : VersioningOverride;

        /// <summary>
        /// The workflow will auto-upgrade to the current deployment version on the next workflow
        /// task.
        /// </summary>
        public sealed record AutoUpgradeVersioningOverride() : VersioningOverride;
    }
}
