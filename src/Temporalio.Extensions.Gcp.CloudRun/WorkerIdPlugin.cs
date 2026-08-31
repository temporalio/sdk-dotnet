using System;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Worker;

namespace Temporalio.Extensions.Gcp.CloudRun
{
    /// <summary>
    /// Temporal client and worker plugin that derives the worker identity and the worker deployment
    /// version from Google Cloud Run instance metadata, on both Cloud Run worker pools and services.
    /// </summary>
    /// <remarks>
    /// Register a single instance on <see cref="TemporalClientConnectOptions.Plugins" />. Because
    /// the plugin implements both the client and worker plugin interfaces, it propagates to workers
    /// created from the connected client automatically:
    /// <list type="bullet">
    /// <item><description>
    /// At connect time it fetches the Cloud Run metadata once (caching it) and, unless an identity
    /// was already configured, sets
    /// <see cref="Temporalio.Client.TemporalConnectionOptions.Identity" /> to the Cloud Run worker
    /// identity, so an explicitly configured identity always wins.
    /// </description></item>
    /// <item><description>
    /// When a worker is created it sets <see cref="TemporalWorkerOptions.DeploymentOptions" /> to the
    /// Cloud Run worker deployment version with worker versioning enabled and a
    /// <see cref="VersioningBehavior.Pinned" /> default (a per-workflow behavior still wins).
    /// </description></item>
    /// </list>
    /// The metadata fetch fails fast with an <see cref="InvalidOperationException" /> at connect time
    /// when the process is not running on a Cloud Run worker pool or service. Tests and advanced
    /// users can bypass the real fetch with <see cref="WorkerIdPluginOptions" />.
    /// WARNING: Google Cloud Run support is experimental.
    /// </remarks>
    public class WorkerIdPlugin : SimplePlugin
    {
        private readonly Uri? metadataUri;
        private readonly TimeSpan? timeout;
        private readonly object metadataLock = new();
        private GoogleCloudRunMetadata? metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerIdPlugin"/> class using the default
        /// Cloud Run metadata server URI and timeout.
        /// </summary>
        public WorkerIdPlugin()
            : this(new WorkerIdPluginOptions())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerIdPlugin"/> class.
        /// </summary>
        /// <param name="options">
        /// Plugin options, including optional pre-fetched metadata or metadata server URI and
        /// timeout overrides.
        /// </param>
        public WorkerIdPlugin(WorkerIdPluginOptions options)
            : base("Temporalio.Extensions.Gcp.CloudRun.WorkerIdPlugin")
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            metadata = options.Metadata;
            metadataUri = options.MetadataUri;
            timeout = options.Timeout;
        }

        /// <inheritdoc />
        public override async Task<TemporalConnection> ConnectAsync(
            TemporalClientConnectOptions options,
            Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation)
        {
            var resolved = await GetMetadataAsync(CancellationToken.None).ConfigureAwait(false);
            if (string.IsNullOrEmpty(options.Identity))
            {
                options.Identity = resolved.WorkerIdentity;
            }
            return await continuation(options).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override void ConfigureWorker(TemporalWorkerOptions options)
        {
            base.ConfigureWorker(options);

            GoogleCloudRunMetadata resolved;
            lock (metadataLock)
            {
                resolved = metadata ?? throw new InvalidOperationException(
                    "Cloud Run metadata has not been fetched yet. Register this plugin on the " +
                    "client via TemporalClientConnectOptions.Plugins and connect with " +
                    "TemporalClient.ConnectAsync before creating a worker, or provide pre-fetched " +
                    "metadata through WorkerIdPluginOptions.Metadata.");
            }

            options.DeploymentOptions = new WorkerDeploymentOptions(
                resolved.ToWorkerDeploymentVersion(),
                useWorkerVersioning: true)
            {
                DefaultVersioningBehavior = VersioningBehavior.Pinned,
            };
        }

        /// <summary>
        /// Return the cached Cloud Run metadata, fetching and caching it on the first call.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved Cloud Run metadata.</returns>
        private async Task<GoogleCloudRunMetadata> GetMetadataAsync(
            CancellationToken cancellationToken)
        {
            lock (metadataLock)
            {
                if (metadata is { } cached)
                {
                    return cached;
                }
            }

            var fetched = await GoogleCloudRunMetadata.FetchWithDefaultsAsync(
                metadataUri, timeout, cancellationToken).ConfigureAwait(false);

            lock (metadataLock)
            {
                // Reuse an already-cached value if a concurrent connect populated it first, so the
                // identity stays stable across connects that share this plugin instance.
                return metadata ??= fetched;
            }
        }
    }
}
