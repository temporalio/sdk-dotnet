using Microsoft.Extensions.Hosting;
using Temporalio.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Temporal extensions for <see cref="IServiceCollection" />.
    /// </summary>
    public static class TemporalHostingServiceCollectionExtensions
    {
        /// <summary>
        /// Add a hosted Temporal worker service as a <see cref="IHostedService" /> that contains
        /// its own client that connects with the given target and namespace. To use an injected
        /// <see cref="Temporalio.Client.ITemporalClient" />, use
        /// <see cref="AddHostedTemporalWorker(IServiceCollection, string)" />. The worker service
        /// will be registered as a singleton. The result is an options builder that can be used to
        /// configure the service.
        /// </summary>
        /// <param name="services">Service collection to create hosted worker on.</param>
        /// <param name="clientTargetHost">Client target host to connect to when starting the
        /// worker.</param>
        /// <param name="clientNamespace">Client namespace to connect to when starting the worker.
        /// </param>
        /// <param name="taskQueue">Task queue for the worker.</param>
        /// <returns>Options builder to configure the service.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddHostedTemporalWorker(
            this IServiceCollection services,
            string clientTargetHost,
            string clientNamespace,
            string taskQueue) =>
            services.AddHostedTemporalWorker(taskQueue).ConfigureOptions(options =>
                options.ClientOptions = new(clientTargetHost) { Namespace = clientNamespace });

        /// <summary>
        /// Add a hosted Temporal worker service as a <see cref="IHostedService" /> that expects
        /// an injected <see cref="Temporalio.Client.ITemporalClient" /> (or the returned builder
        /// can have client options populated). Use
        /// <see cref="AddHostedTemporalWorker(IServiceCollection, string, string, string)" /> to
        /// not expect an injected instance and instead connect to a client on worker start. The
        /// worker service will be registered as a singleton. The result is an options builder that
        /// can be used to configure the service.
        /// </summary>
        /// <param name="services">Service collection to create hosted worker on.</param>
        /// <param name="taskQueue">Task queue for the worker.</param>
        /// <returns>Options builder to configure the service.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddHostedTemporalWorker(
            this IServiceCollection services, string taskQueue)
        {
            // We have to use AddSingleton instead of AddHostedService because the latter does
            // not allow us to register multiple of the same type, see
            // https://github.com/dotnet/runtime/issues/38751
            services.AddSingleton<IHostedService>(provider =>
                ActivatorUtilities.CreateInstance<TemporalWorkerService>(provider, taskQueue));
            return new TemporalWorkerServiceOptionsBuilder(taskQueue, services).ConfigureOptions(
                options => options.TaskQueue = taskQueue);
        }
    }
}