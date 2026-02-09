using System;
using System.Collections.Generic;
using System.Linq;

namespace Temporalio.Client
{
    /// <summary>
    /// Represents the sets of compatible Build ID versions associated with some Task Queue, as
    /// fetched by <see cref="ITemporalClient.GetWorkerBuildIdCompatibilityAsync"/>.
    /// </summary>
    /// <param name="VersionSets">The sets of compatible versions in the Task Queue.</param>
    [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
    public record WorkerBuildIdVersionSets(IReadOnlyCollection<BuildIdVersionSet> VersionSets)
    {
        /// <summary>
        /// Gets the default Build ID for this Task Queue.
        /// </summary>
        /// <returns>That Build ID.</returns>
        public string DefaultBuildId => DefaultSet.Default;

        /// <summary>
        /// Gets the default compatible set for this Task Queue.
        /// </summary>
        /// <returns>That set.</returns>
        public BuildIdVersionSet DefaultSet => this.VersionSets.Last();

        /// <summary>
        /// Convert from proto.
        /// </summary>
        /// <param name="proto">Proto.</param>
        /// <returns>Converted value, if there are any sets, otherwise null.</returns>
        internal static WorkerBuildIdVersionSets? FromProto(
            Api.WorkflowService.V1.GetWorkerBuildIdCompatibilityResponse proto)
        {
            var sets = proto.MajorVersionSets.Select(vs => new BuildIdVersionSet(vs.BuildIds)).ToList();
            if (sets.Count == 0)
            {
                return null;
            }

            return new(sets);
        }
    }

    /// <summary>
    /// A set of Build IDs which are compatible with each other.
    /// </summary>
    /// <param name="BuildIds">The Build IDs.</param>
    [Obsolete("Use the Worker Deployment API instead. See https://docs.temporal.io/worker-deployments")]
    public record BuildIdVersionSet(IReadOnlyCollection<string> BuildIds)
    {
        /// <summary>Gets the default Build ID for this set.</summary>
        /// <returns>That Build ID.</returns>
        public string Default => this.BuildIds.Last();
    }
}