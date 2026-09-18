using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Extensions.Gcp.CloudRun.Id
{
    /// <summary>
    /// Reads Google Cloud Run instance metadata to derive a Temporal worker identity on both Cloud Run worker pools and services.
    /// </summary>
    /// <remarks>
    /// Most callers should register a <see cref="CloudRunIdPlugin" />
    /// on <see cref="Temporalio.Client.TemporalClientConnectOptions.Plugins" />, which fetches this
    /// metadata once at connect time and applies the worker identity automatically. This type is
    /// exposed for advanced use, for example reading <see cref="Identity" /> directly.
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
        public string Identity
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
        /// Fetch Cloud Run metadata. The metadata server URI and request timeout default when null.
        /// </summary>
        /// <param name="metadataUri">Metadata server URI for the instance id, or null for the default.</param>
        /// <param name="timeout">Timeout for the metadata request, or null for the default.</param>
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
        /// Thrown when the instance id cannot be read from the metadata server (it is unreachable,
        /// times out, or returns an empty id), which usually means the process is not running on a
        /// Google Cloud Run worker pool or service.
        /// </exception>
        public static async Task<GoogleCloudRunMetadata> FetchAsync(
            Uri? metadataUri = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var uri = metadataUri ?? DefaultMetadataUri;
            var name = FirstNonEmptyEnvironmentVariable(
                WorkerPoolEnvironmentVariable,
                ServiceEnvironmentVariable);
            var revision = FirstNonEmptyEnvironmentVariable(
                WorkerPoolRevisionEnvironmentVariable,
                ServiceRevisionEnvironmentVariable);

            using var httpClient = new HttpClient { Timeout = timeout ?? DefaultTimeout };
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
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
                    $"{uri}. This process may not be running on a Google Cloud Run worker pool or " +
                    "service.",
                    e);
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "Timed out reading the Google Cloud Run instance id from the metadata server " +
                    $"at {uri}. This process may not be running on a Google Cloud Run worker pool " +
                    "or service.",
                    e);
            }

            if (instanceId.Length == 0)
            {
                throw new InvalidOperationException(
                    $"The Google Cloud Run metadata server at {uri} returned an empty instance id. " +
                    "This process may not be running on a Google Cloud Run worker pool or service.");
            }

            return new GoogleCloudRunMetadata(instanceId, name, revision);
        }

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
