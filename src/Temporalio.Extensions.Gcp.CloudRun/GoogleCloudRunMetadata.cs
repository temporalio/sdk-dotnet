using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Common;

namespace Temporalio.Extensions.Gcp.CloudRun
{
    /// <summary>
    /// Reads Google Cloud Run instance metadata to derive a Temporal worker identity and a
    /// <see cref="WorkerDeploymentVersion" /> for a long-lived worker, on both Cloud Run worker
    /// pools and services.
    /// </summary>
    /// <remarks>
    /// Cloud Run runs a long-lived container, so unlike the AWS Lambda extension this is a metadata
    /// helper rather than a worker wrapper. Most callers should register a <see cref="CloudRunPlugin" />
    /// on <see cref="Temporalio.Client.TemporalClientConnectOptions.Plugins" />, which fetches this
    /// metadata once at connect time and applies the worker identity and deployment version
    /// automatically. This type is exposed for advanced use, for example reading
    /// <see cref="WorkerIdentity" /> or <see cref="ToWorkerDeploymentVersion" /> directly.
    /// WARNING: Google Cloud Run support is experimental.
    /// </remarks>
    public sealed class GoogleCloudRunMetadata
    {
        private const string WorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
        private const string ServiceEnvironmentVariable = "K_SERVICE";
        private const string WorkerPoolRevisionEnvironmentVariable = "CLOUD_RUN_REVISION";
        private const string ServiceRevisionEnvironmentVariable = "K_REVISION";
        private const string MetadataFlavorHeader = "Metadata-Flavor";
        private const string MetadataFlavorValue = "Google";

        private static readonly Uri DefaultMetadataUri =
            new Uri("http://metadata.google.internal/computeMetadata/v1/instance/id");

        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="GoogleCloudRunMetadata"/> class.
        /// </summary>
        /// <param name="instanceId">Cloud Run instance id from the metadata server.</param>
        /// <param name="name">Cloud Run worker pool or service name.</param>
        /// <param name="revision">Cloud Run revision name.</param>
        internal GoogleCloudRunMetadata(string instanceId, string name, string revision)
        {
            InstanceId = instanceId;
            Name = name;
            Revision = revision;
        }

        /// <summary>
        /// Gets the Cloud Run instance id read from the metadata server.
        /// </summary>
        public string InstanceId { get; }

        /// <summary>
        /// Gets the Cloud Run worker pool or service name, resolved from the
        /// <c>CLOUD_RUN_WORKER_POOL</c> environment variable and then the <c>K_SERVICE</c>
        /// environment variable, or an empty string if neither is set.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the Cloud Run revision name, resolved from the <c>CLOUD_RUN_REVISION</c> environment
        /// variable and then the <c>K_REVISION</c> environment variable, or an empty string if
        /// neither is set.
        /// </summary>
        public string Revision { get; }

        /// <summary>
        /// Gets the worker identity derived from the metadata. This is
        /// <c>{InstanceId}@{Revision}</c>, falling back to <c>{InstanceId}@{Name}</c> when the
        /// revision is empty, or just <c>{InstanceId}</c> when both are empty.
        /// </summary>
        public string WorkerIdentity
        {
            get
            {
                if (!string.IsNullOrEmpty(Revision))
                {
                    return $"{InstanceId}@{Revision}";
                }
                if (!string.IsNullOrEmpty(Name))
                {
                    return $"{InstanceId}@{Name}";
                }
                return InstanceId;
            }
        }

        /// <summary>
        /// Fetch Cloud Run metadata using the default metadata server URI and timeout.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved Cloud Run metadata.</returns>
        public static Task<GoogleCloudRunMetadata> FetchAsync(
            CancellationToken cancellationToken = default) =>
            FetchAsync(DefaultMetadataUri, DefaultTimeout, cancellationToken);

        /// <summary>
        /// Fetch Cloud Run metadata from the given metadata server URI.
        /// </summary>
        /// <param name="metadataUri">Metadata server URI for the instance id.</param>
        /// <param name="timeout">Timeout for the metadata request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved Cloud Run metadata.</returns>
        /// <remarks>
        /// The name and revision are read from environment variables that Cloud Run injects:
        /// <c>CLOUD_RUN_WORKER_POOL</c> then <c>K_SERVICE</c> for the name, and
        /// <c>CLOUD_RUN_REVISION</c> then <c>K_REVISION</c> for the revision. Worker pools receive
        /// the <c>CLOUD_RUN_*</c> variables and services receive the <c>K_*</c> variables. The
        /// instance id is read from the metadata server, which is available on both and requires the
        /// <c>Metadata-Flavor: Google</c> request header.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the instance id cannot be read from the metadata server, which usually means
        /// the process is not running on a Google Cloud Run worker pool or service.
        /// </exception>
        public static async Task<GoogleCloudRunMetadata> FetchAsync(
            Uri metadataUri,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var name = FirstNonEmptyEnvironmentVariable(
                WorkerPoolEnvironmentVariable,
                ServiceEnvironmentVariable);
            var revision = FirstNonEmptyEnvironmentVariable(
                WorkerPoolRevisionEnvironmentVariable,
                ServiceRevisionEnvironmentVariable);

            using var httpClient = new HttpClient { Timeout = timeout };
            using var request = new HttpRequestMessage(HttpMethod.Get, metadataUri);
            request.Headers.Add(MetadataFlavorHeader, MetadataFlavorValue);

            string instanceId;
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken).
                    ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                instanceId = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).
                    Trim();
            }
            catch (HttpRequestException e)
            {
                throw new InvalidOperationException(
                    "Failed to read the Google Cloud Run instance id from the metadata server at " +
                    $"{metadataUri}. This process may not be running on a Google Cloud Run worker " +
                    "pool or service.",
                    e);
            }

            return new GoogleCloudRunMetadata(instanceId, name, revision);
        }

        /// <summary>
        /// Build a <see cref="WorkerDeploymentVersion" /> from the Cloud Run name and revision.
        /// </summary>
        /// <returns>
        /// A version whose deployment name is the Cloud Run worker pool or service name and whose
        /// build id is the Cloud Run revision.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the name or revision is empty, which usually means the process is not running
        /// on a Google Cloud Run worker pool or service.
        /// </exception>
        public WorkerDeploymentVersion ToWorkerDeploymentVersion()
        {
            if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(Revision))
            {
                throw new InvalidOperationException(
                    "Cannot build a WorkerDeploymentVersion without both a Cloud Run name and " +
                    "revision. This process may not be running on a Google Cloud Run worker pool " +
                    "or service.");
            }
            return new WorkerDeploymentVersion(Name, Revision);
        }

        /// <summary>
        /// Fetch Cloud Run metadata, filling in the default metadata server URI and timeout for any
        /// argument that is null.
        /// </summary>
        /// <param name="metadataUri">Metadata server URI, or null for the default.</param>
        /// <param name="timeout">Metadata request timeout, or null for the default.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The resolved Cloud Run metadata.</returns>
        internal static Task<GoogleCloudRunMetadata> FetchWithDefaultsAsync(
            Uri? metadataUri,
            TimeSpan? timeout,
            CancellationToken cancellationToken) =>
            FetchAsync(
                metadataUri ?? DefaultMetadataUri,
                timeout ?? DefaultTimeout,
                cancellationToken);

        private static string FirstNonEmptyEnvironmentVariable(params string[] names)
        {
            foreach (var name in names)
            {
                var value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
                if (value.Length > 0)
                {
                    return value;
                }
            }
            return string.Empty;
        }
    }
}
