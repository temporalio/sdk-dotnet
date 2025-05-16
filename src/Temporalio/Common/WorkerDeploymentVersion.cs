using System;

namespace Temporalio.Common
{
    /// <summary>
    /// Represents the version of a specific worker deployment.
    /// </summary>
    /// <remarks>WARNING: Deployment-based versioning is experimental and APIs may change.</remarks>
    public sealed record WorkerDeploymentVersion(string DeploymentName, string BuildId)
    {
        private static readonly char[] Separator = new char[] { '.' };

        /// <summary>
        /// Parse a version from a canonical string, which must be in the format:
        /// <code>deployment_name.build_id</code>
        /// Deployment name must not have a `.` in it.
        /// </summary>
        /// <param name="canonical">The canonical string to parse.</param>
        /// <returns>A new <see cref="WorkerDeploymentVersion"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the canonical string is not in the expected format.</exception>
        public static WorkerDeploymentVersion FromCanonicalString(string canonical)
        {
            string[] parts = canonical.Split(Separator, 2);
            return parts.Length != 2
                ? throw new ArgumentException(
                    $"Cannot parse version string: {canonical}, must be in format <deployment_name>.<build_id>",
                    nameof(canonical))
                : new WorkerDeploymentVersion(parts[0], parts[1]);
        }

        /// <summary>
        /// Returns the canonical string representation of the version.
        /// </summary>
        /// <returns>The canonical string representation.</returns>
        public string ToCanonicalString() => $"{DeploymentName}.{BuildId}";

        /// <summary>
        /// Returns a new <see cref="WorkerDeploymentVersion"/> instance from a bridge version.
        /// </summary>
        /// <param name="bridgeVersion">The bridge version to convert.</param>
        /// <returns>A new <see cref="WorkerDeploymentVersion"/> instance.</returns>
        internal static WorkerDeploymentVersion FromBridge(
            Temporalio.Bridge.Api.Common.WorkerDeploymentVersion bridgeVersion) =>
        new(bridgeVersion.DeploymentName, bridgeVersion.BuildId);

        /// <summary>
        /// Converts this version to a proto.
        /// </summary>
        /// <returns>The proto representation.</returns>
        internal Temporalio.Api.Deployment.V1.WorkerDeploymentVersion ToProto() => new()
        {
            BuildId = BuildId,
            DeploymentName = DeploymentName,
        };
    }
}
