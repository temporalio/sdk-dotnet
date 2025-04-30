using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Implementation of <see cref="ITemporalWorkerServiceOptionsBuilder" />.
    /// </summary>
    public class TemporalWorkerServiceOptionsBuilder : ITemporalWorkerServiceOptionsBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerServiceOptionsBuilder" />
        /// class.
        /// </summary>
        /// <param name="taskQueue">Task queue for the worker.</param>
        /// <param name="services">Service collection being configured.</param>
        public TemporalWorkerServiceOptionsBuilder(string taskQueue, IServiceCollection services)
                    : this(taskQueue, (string?)null, services)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerServiceOptionsBuilder" />
        /// class.
        /// </summary>
        /// <param name="taskQueue">Task queue for the worker.</param>
        /// <param name="buildId">Build ID for the worker.</param>
        /// <param name="services">Service collection being configured.</param>
        public TemporalWorkerServiceOptionsBuilder(string taskQueue, string? buildId, IServiceCollection services)
        {
            TaskQueue = taskQueue;
            BuildId = buildId;
            Services = services;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerServiceOptionsBuilder" />
        /// class.
        /// </summary>
        /// <param name="taskQueue">Task queue for the worker.</param>
        /// <param name="deploymentOptions">Deployment options for the worker.</param>
        /// <param name="services">Service collection being configured.</param>
        public TemporalWorkerServiceOptionsBuilder(string taskQueue, WorkerDeploymentOptions? deploymentOptions, IServiceCollection services)
        {
            TaskQueue = taskQueue;
            DeploymentOptions = deploymentOptions;
            Services = services;
        }

        /// <inheritdoc />
        public string TaskQueue { get; private init; }

        /// <inheritdoc />
        public string? BuildId { get; private init; }

        /// <inheritdoc />
        public WorkerDeploymentOptions? DeploymentOptions { get; private init; }

        /// <inheritdoc />
        public IServiceCollection Services { get; private init; }
    }
}
