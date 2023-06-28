using Microsoft.Extensions.DependencyInjection;

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
        {
            TaskQueue = taskQueue;
            Services = services;
        }

        /// <inheritdoc />
        public string TaskQueue { get; private init; }

        /// <inheritdoc />
        public IServiceCollection Services { get; private init; }
    }
}