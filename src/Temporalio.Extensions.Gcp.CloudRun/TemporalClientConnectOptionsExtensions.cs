using System;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Client;

namespace Temporalio.Extensions.Gcp.CloudRun
{
    /// <summary>
    /// Google Cloud Run extensions for <see cref="TemporalClientConnectOptions" />.
    /// </summary>
    /// <remarks>WARNING: Google Cloud Run support is experimental.</remarks>
    public static class TemporalClientConnectOptionsExtensions
    {
        /// <summary>
        /// Fetch Google Cloud Run metadata and apply its worker identity to the client options.
        /// </summary>
        /// <param name="options">Client connection options to mutate.</param>
        /// <param name="metadataUri">
        /// Metadata server URI for the instance id. If null, the default Cloud Run instance id
        /// endpoint is used.
        /// </param>
        /// <param name="timeout">
        /// Timeout for the metadata request. If null, a default of two seconds is used.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The fetched Cloud Run metadata.</returns>
        /// <remarks>
        /// <see cref="Temporalio.Client.TemporalConnectionOptions.Identity" /> is only set when it is
        /// null or empty, so an explicitly configured identity wins. The returned metadata can be
        /// passed to
        /// <see cref="TemporalWorkerOptionsExtensions.ApplyGoogleCloudRunDefaults" />.
        /// WARNING: Google Cloud Run support is experimental.
        /// </remarks>
        public static async Task<GoogleCloudRunMetadata> ApplyGoogleCloudRunDefaultsAsync(
            this TemporalClientConnectOptions options,
            Uri? metadataUri = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var metadata = await GoogleCloudRunMetadata.FetchWithDefaultsAsync(
                metadataUri,
                timeout,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(options.Identity))
            {
                options.Identity = metadata.WorkerIdentity;
            }
            return metadata;
        }
    }
}
