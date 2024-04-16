using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;
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
        /// <see cref="ITemporalClient" />, use
        /// <see cref="AddHostedTemporalWorker(IServiceCollection, string, string?)" />. The worker
        /// service will be registered as a singleton. The result is an options builder that can be
        /// used to configure the service.
        /// </summary>
        /// <param name="services">Service collection to create hosted worker on.</param>
        /// <param name="clientTargetHost">Client target host to connect to when starting the
        /// worker.</param>
        /// <param name="clientNamespace">Client namespace to connect to when starting the worker.
        /// </param>
        /// <param name="taskQueue">Task queue for the worker.</param>
        /// <param name="buildId">
        /// Build ID for the worker. Set to non-null to opt in to versioning. If versioning is
        /// wanted, this must be set here and not later via configure options. This is because the
        /// combination of task queue and build ID make up the unique identifier for a worker in the
        /// service collection.
        /// </param>
        /// <returns>Options builder to configure the service.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddHostedTemporalWorker(
            this IServiceCollection services,
            string clientTargetHost,
            string clientNamespace,
            string taskQueue,
            string? buildId = null) =>
            services.AddHostedTemporalWorker(taskQueue, buildId).ConfigureOptions(options =>
                options.ClientOptions = new(clientTargetHost) { Namespace = clientNamespace });

        /// <summary>
        /// Add a hosted Temporal worker service as a <see cref="IHostedService" /> that expects
        /// an injected <see cref="ITemporalClient" /> (or the returned builder
        /// can have client options populated). Use
        /// <see cref="AddHostedTemporalWorker(IServiceCollection, string, string, string, string?)" />
        /// to not expect an injected instance and instead connect to a client on worker start. The
        /// worker service will be registered as a singleton. The result is an options builder that
        /// can be used to configure the service.
        /// </summary>
        /// <param name="services">Service collection to create hosted worker on.</param>
        /// <param name="taskQueue">Task queue for the worker.</param>
        /// <param name="buildId">
        /// Build ID for the worker. Set to non-null to opt in to versioning. If versioning is
        /// wanted, this must be set here and not later via configure options. This is because the
        /// combination of task queue and build ID make up the unique identifier for a worker in the
        /// service collection.
        /// </param>
        /// <returns>Options builder to configure the service.</returns>
        public static ITemporalWorkerServiceOptionsBuilder AddHostedTemporalWorker(
            this IServiceCollection services, string taskQueue, string? buildId = null)
        {
            // We have to use AddSingleton instead of AddHostedService because the latter does
            // not allow us to register multiple of the same type, see
            // https://github.com/dotnet/runtime/issues/38751.
            services.AddSingleton<IHostedService>(provider =>
                ActivatorUtilities.CreateInstance<TemporalWorkerService>(
                    provider, (TaskQueue: taskQueue, BuildId: buildId)));
            return new TemporalWorkerServiceOptionsBuilder(taskQueue, buildId, services).ConfigureOptions(
                options =>
                {
                    options.TaskQueue = taskQueue;
                    options.BuildId = buildId;
                    options.UseWorkerVersioning = buildId != null;
                },
                // Disallow duplicate options registrations because that means multiple worker
                // services with the same task queue + build ID were added.
                disallowDuplicates: true);
        }

        /// <summary>
        /// Adds a singleton <see cref="ITemporalClient" /> via
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService}(IServiceCollection, Func{IServiceProvider, TService})" />
        /// using a lazy client created with <see cref="TemporalClient.CreateLazy" />. The resulting
        /// builder can be used to configure the client as can any options approach that alters
        /// <see cref="TemporalClientConnectOptions" />. If a logging factory is on the container,
        /// it will be set on the client.
        /// </summary>
        /// <param name="services">Service collection to add Temporal client to.</param>
        /// <param name="clientTargetHost">If set, the host to connect to.</param>
        /// <param name="clientNamespace">If set, the namespace for the client.</param>
        /// <returns>Options builder for setting client options.</returns>
        /// <remarks>
        /// This client can be used with <see cref="TemporalWorkerService" /> from
        /// <c>AddHostedTemporalWorker</c> but not <see cref="Temporalio.Worker.TemporalWorker" />
        /// directly because lazy unconnected clients can't be used directly with those workers.
        /// </remarks>
        public static OptionsBuilder<TemporalClientConnectOptions> AddTemporalClient(
            this IServiceCollection services,
            string? clientTargetHost = null,
            string? clientNamespace = null)
        {
            services.TryAddSingleton<ITemporalClient>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>();
                return TemporalClient.CreateLazy(options.Value);
            });
            var builder = services.AddOptions<TemporalClientConnectOptions>();
            if (clientTargetHost != null || clientNamespace != null)
            {
                builder.Configure(options =>
                {
                    options.TargetHost = clientTargetHost;
                    if (clientNamespace != null)
                    {
                        options.Namespace = clientNamespace;
                    }
                });
            }
            builder.Configure<IServiceProvider>((options, provider) =>
            {
                if (provider.GetService<ILoggerFactory>() is { } loggerFactory)
                {
                    options.LoggerFactory = loggerFactory;
                }
            });
            return builder;
        }

        /// <summary>
        /// Adds a singleton <see cref="ITemporalClient" /> via
        /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService}(IServiceCollection, Func{IServiceProvider, TService})" />
        /// using a lazy client created with <see cref="TemporalClient.CreateLazy" />. The action
        /// can be used to configure the client as can any options approach that alters
        /// <see cref="TemporalClientConnectOptions" />. If a logging factory is on the container,
        /// it will be set on the client.
        /// </summary>
        /// <param name="services">Service collection to add Temporal client to.</param>
        /// <param name="configureClient">Action to configure client options.</param>
        /// <returns>The given service collection for chaining.</returns>
        public static IServiceCollection AddTemporalClient(
            this IServiceCollection services, Action<TemporalClientConnectOptions> configureClient)
        {
            services.AddTemporalClient().Configure(configureClient);
            return services;
        }
    }
}