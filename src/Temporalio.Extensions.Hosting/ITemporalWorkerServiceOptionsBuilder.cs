using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Interface for configuring <see cref="TemporalWorkerServiceOptions" /> for
    /// <see cref="TemporalWorkerService" />. Methods for using this are as extensions in this
    /// namespace.
    /// </summary>
    public interface ITemporalWorkerServiceOptionsBuilder
    {
        /// <summary>
        /// Gets the task queue for this worker service.
        /// </summary>
        string TaskQueue { get; }

        /// <summary>
        /// Gets the build ID for this worker service.
        /// Exclusive with DeploymentOptions.
        /// </summary>
        string? BuildId { get; }

        /// <summary>
        /// Gets the deployment options for this worker service.
        /// Exclusive with BuildId.
        /// </summary>
        WorkerDeploymentOptions? DeploymentOptions { get; }

        /// <summary>
        /// Gets the service collection being configured.
        /// </summary>
        IServiceCollection Services { get; }
    }
}
