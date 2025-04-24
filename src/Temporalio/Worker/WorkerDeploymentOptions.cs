using Temporalio.Common;

namespace Temporalio.Worker
{
    /// <summary>
    /// Options for configuring the Worker Versioning feature.
    /// </summary>
    /// <remarks>WARNING: Deployment-based versioning is experimental and APIs may change.</remarks>
    public class WorkerDeploymentOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerDeploymentOptions"/> class.
        /// </summary>
        /// <param name="version">The worker deployment version.</param>
        /// <param name="useWorkerVersioning">Whether worker versioning is enabled.</param>
        public WorkerDeploymentOptions(WorkerDeploymentVersion version, bool useWorkerVersioning)
        {
            Version = version;
            UseWorkerVersioning = useWorkerVersioning;
        }

        /// <summary>
        /// Gets or sets the worker deployment version.
        /// </summary>
        public WorkerDeploymentVersion Version { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether worker versioning is enabled.
        /// </summary>
        public bool UseWorkerVersioning { get; set; }

        /// <summary>
        /// Gets or sets the default versioning behavior.
        /// </summary>
        public VersioningBehavior DefaultVersioningBehavior { get; set; } = VersioningBehavior.Unspecified;
    }
}
