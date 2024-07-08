using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client
{
    /// <summary>
    /// Client for Temporal Cloud Operations.
    /// </summary>
    /// <remarks>
    /// Clients are thread-safe and are encouraged to be reused to properly reuse the underlying
    /// connection.
    /// </remarks>
    /// <remarks>
    /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
    /// </remarks>
    public class TemporalCloudOperationsClient : ITemporalCloudOperationsClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalCloudOperationsClient"/> class from
        /// an existing connection.
        /// </summary>
        /// <param name="connection">Connection for this client.</param>
        /// <remarks>
        /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
        /// </remarks>
        public TemporalCloudOperationsClient(ITemporalConnection connection) =>
            Connection = connection;

        /// <inheritdoc />
        public ITemporalConnection Connection { get; private init; }

        /// <inheritdoc />
        public CloudService CloudService => Connection.CloudService;

        /// <summary>
        /// Connect to the Temporal Cloud Operations API.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The connected client.</returns>
        /// <remarks>
        /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
        /// </remarks>
        public static async Task<TemporalCloudOperationsClient> ConnectAsync(
            TemporalCloudOperationsClientConnectOptions options) =>
            new(await TemporalConnection.ConnectAsync(
                MaybeWithVersionHeader(options)).ConfigureAwait(false));

        /// <summary>
        /// Create a client to the Temporal Cloud Operations API that does not connect until first
        /// call.
        /// </summary>
        /// <param name="options">Options for connecting.</param>
        /// <returns>The not-yet-connected client.</returns>
        /// <remarks>
        /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
        /// </remarks>
        public static TemporalCloudOperationsClient CreateLazy(
            TemporalCloudOperationsClientConnectOptions options) =>
            new(TemporalConnection.CreateLazy(
                MaybeWithVersionHeader(options)));

        private static TemporalCloudOperationsClientConnectOptions MaybeWithVersionHeader(
            TemporalCloudOperationsClientConnectOptions options)
        {
            // If there is a version, add it as RPC metadata (cloning first)
            if (options.Version is not { } version)
            {
                return options;
            }
            var newMetadata = new Dictionary<string, string>
            {
                ["temporal-cloud-api-version"] = version,
            };
            if (options.RpcMetadata is { } existing)
            {
                foreach (var kvp in existing)
                {
                    newMetadata[kvp.Key] = kvp.Value;
                }
            }
            options = (TemporalCloudOperationsClientConnectOptions)options.Clone();
            options.RpcMetadata = newMetadata;
            return options;
        }
    }
}