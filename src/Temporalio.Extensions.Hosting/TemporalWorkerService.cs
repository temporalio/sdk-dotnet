using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Worker;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Temporal worker implementation as a <see cref="BackgroundService" />.
    /// </summary>
    public class TemporalWorkerService : BackgroundService
    {
        // These two are mutually exclusive
        private readonly TemporalClientConnectOptions? newClientOptions;
        private readonly ITemporalClient? existingClient;
        private readonly TemporalWorkerOptions workerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerService"/> class using
        /// service options. This will create a client on worker start and therefore
        /// <see cref="TemporalWorkerServiceOptions.ClientOptions" /> must be non-null. To provide
        /// a client, use
        /// <see cref="TemporalWorkerService(ITemporalClient, TemporalWorkerOptions)" />.
        /// </summary>
        /// <param name="options">Options to use to create the worker service.</param>
        public TemporalWorkerService(TemporalWorkerServiceOptions options)
        {
            newClientOptions = options.ClientOptions ?? throw new ArgumentException(
                "Client options is required", nameof(options));
            workerOptions = options;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerService"/> class using
        /// client options and worker options. This will create a client on worker start. To provide
        /// a client, use
        /// <see cref="TemporalWorkerService(ITemporalClient, TemporalWorkerOptions)" />.
        /// </summary>
        /// <param name="clientOptions">Options to connect a client.</param>
        /// <param name="workerOptions">Options for the worker.</param>
        public TemporalWorkerService(
            TemporalClientConnectOptions clientOptions,
            TemporalWorkerOptions workerOptions)
        {
            newClientOptions = clientOptions;
            this.workerOptions = workerOptions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerService"/> class using
        /// an existing client and worker options.
        /// </summary>
        /// <param name="client">Client to use. If this client is lazy and not connected, it will be
        /// connected when this service is run.</param>
        /// <param name="workerOptions">Options for the worker.</param>
        public TemporalWorkerService(
            ITemporalClient client,
            TemporalWorkerOptions workerOptions)
        {
            existingClient = client;
            this.workerOptions = workerOptions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerService"/> class using
        /// options and possibly an existing client. This constructor is mostly used by DI
        /// containers. The task queue is used as the name on the options monitor to lookup the
        /// options for the worker service.
        /// </summary>
        /// <param name="taskQueue">Task queue which is the options name.</param>
        /// <param name="optionsMonitor">Used to lookup the options to build the worker with.
        /// </param>
        /// <param name="existingClient">Existing client to use if the options don't specify
        /// client connection options (connected when run if lazy and not connected).</param>
        /// <param name="loggerFactory">Logger factory to use if there is no logger factory on the
        /// existing client, or the client connect options, or the worker options.</param>
        [ActivatorUtilitiesConstructor]
        public TemporalWorkerService(
            string taskQueue,
            IOptionsMonitor<TemporalWorkerServiceOptions> optionsMonitor,
            ITemporalClient? existingClient = null,
            ILoggerFactory? loggerFactory = null)
        {
            var options = (TemporalWorkerServiceOptions)optionsMonitor.Get(taskQueue).Clone();
            newClientOptions = options.ClientOptions;
            if (newClientOptions == null)
            {
                this.existingClient = existingClient;
                if (existingClient == null)
                {
                    throw new InvalidOperationException(
                        "Cannot start worker service with no client and no client connect options");
                }
            }

            workerOptions = options;
            // If a logging factory is provided and it is not on the client options already or
            // worker options already, set it on the worker options
            if (loggerFactory != null &&
                workerOptions.LoggerFactory == null &&
                newClientOptions?.LoggerFactory == null &&
                existingClient?.Options?.LoggerFactory == null)
            {
                options.LoggerFactory = loggerFactory;
            }
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var client = existingClient ?? await TemporalClient.ConnectAsync(newClientOptions!).ConfigureAwait(false);
            // Call connect just in case it was a lazy client (no-op if already connected)
            await client.Connection.ConnectAsync().ConfigureAwait(false);
            using var worker = new TemporalWorker(client, workerOptions);
            await worker.ExecuteAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}