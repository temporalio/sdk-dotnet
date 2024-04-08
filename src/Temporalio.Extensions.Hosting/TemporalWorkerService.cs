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
        /// options and possibly an existing client. This constructor was used by DI
        /// containers and is now DEPRECATED.
        /// </summary>
        /// <param name="taskQueue">Task queue which is included in the options name.</param>
        /// <param name="buildId">Build ID which is included in the options name.</param>
        /// <param name="optionsMonitor">Used to lookup the options to build the worker with.
        /// </param>
        /// <param name="existingClient">Existing client to use if the options don't specify
        /// client connection options (connected when run if lazy and not connected).</param>
        /// <param name="loggerFactory">Logger factory to use if not already on the worker options.
        /// The worker options logger factory or this one will be also be used for the client if an
        /// existing client does not exist (regardless of client options' logger factory).</param>
        [Obsolete("Deprecated older form of DI constructor, task queue + build ID tuple one is used instead.")]
        public TemporalWorkerService(
            string taskQueue,
            string? buildId,
            IOptionsMonitor<TemporalWorkerServiceOptions> optionsMonitor,
            ITemporalClient? existingClient = null,
            ILoggerFactory? loggerFactory = null)
            : this((taskQueue, buildId), optionsMonitor, existingClient, loggerFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalWorkerService"/> class using
        /// options and possibly an existing client. This constructor is only for use by DI
        /// containers. The task queue and build ID are used as the name for the options monitor to
        /// lookup the options for the worker service.
        /// </summary>
        /// <param name="taskQueueAndBuildId">Task queue and build ID for the options name.</param>
        /// <param name="optionsMonitor">Used to lookup the options to build the worker with.
        /// </param>
        /// <param name="existingClient">Existing client to use if the options don't specify
        /// client connection options (connected when run if lazy and not connected).</param>
        /// <param name="loggerFactory">Logger factory to use if not already on the worker options.
        /// The worker options logger factory or this one will be also be used for the client if an
        /// existing client does not exist (regardless of client options' logger factory).</param>
        /// <remarks>
        /// WARNING: Do not rely on the signature of this constructor, it is for DI container use
        /// only and may change in incompatible ways.
        /// </remarks>
        [ActivatorUtilitiesConstructor]
        public TemporalWorkerService(
            (string TaskQueue, string? BuildId) taskQueueAndBuildId,
            IOptionsMonitor<TemporalWorkerServiceOptions> optionsMonitor,
            ITemporalClient? existingClient = null,
            ILoggerFactory? loggerFactory = null)
        {
            var options = (TemporalWorkerServiceOptions)optionsMonitor.Get(
                TemporalWorkerServiceOptions.GetUniqueOptionsName(
                    taskQueueAndBuildId.TaskQueue, taskQueueAndBuildId.BuildId)).Clone();

            // Make sure options values match the ones given in constructor
            if (options.TaskQueue != taskQueueAndBuildId.TaskQueue)
            {
                throw new InvalidOperationException(
                    $"Task queue '{taskQueueAndBuildId.TaskQueue}' on constructor different than '{options.TaskQueue}' on options");
            }
            if (options.BuildId != taskQueueAndBuildId.BuildId)
            {
                throw new InvalidOperationException(
                    $"Build ID '{taskQueueAndBuildId.BuildId ?? "<unset>"}' on constructor different than '{options.BuildId ?? "<unset>"}' on options");
            }
            if (options.UseWorkerVersioning != (taskQueueAndBuildId.BuildId != null))
            {
                throw new InvalidOperationException(
                    $"Use versioning option is {options.UseWorkerVersioning}, but constructor expects different");
            }

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

            // Set logger factory on worker options if not already there
            workerOptions.LoggerFactory ??= loggerFactory;
            // Put logger factory, if present, on client options
            if (newClientOptions != null && workerOptions.LoggerFactory != null)
            {
                newClientOptions.LoggerFactory = workerOptions.LoggerFactory;
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