using System;
using Temporalio.Common;
using Temporalio.Worker;

namespace Temporalio.Extensions.Gcp.CloudRun
{
    /// <summary>
    /// Google Cloud Run extensions for <see cref="TemporalWorkerOptions" />.
    /// </summary>
    /// <remarks>WARNING: Google Cloud Run support is experimental.</remarks>
    public static class TemporalWorkerOptionsExtensions
    {
        /// <summary>
        /// Apply the Cloud Run worker deployment version to the worker options.
        /// </summary>
        /// <param name="options">Worker options to mutate.</param>
        /// <param name="metadata">Cloud Run metadata to apply.</param>
        /// <returns>The same <paramref name="options" /> for chaining.</returns>
        /// <remarks>
        /// This sets <see cref="TemporalWorkerOptions.DeploymentOptions" /> with worker versioning
        /// enabled, using the deployment version derived from the Cloud Run name and revision, and
        /// pins workflows to this version by default (<see cref="VersioningBehavior.Pinned" />; a
        /// per-workflow behavior takes precedence). It throws if the name or revision is empty,
        /// which usually means the process is not running on a Google Cloud Run worker pool or
        /// service.
        /// WARNING: Google Cloud Run support is experimental.
        /// </remarks>
        public static TemporalWorkerOptions ApplyGoogleCloudRunDefaults(
            this TemporalWorkerOptions options,
            GoogleCloudRunMetadata metadata)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            options.DeploymentOptions = new WorkerDeploymentOptions(
                metadata.ToWorkerDeploymentVersion(),
                useWorkerVersioning: true)
            {
                DefaultVersioningBehavior = VersioningBehavior.Pinned,
            };
            return options;
        }
    }
}
