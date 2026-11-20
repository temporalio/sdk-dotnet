using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Runtime;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Fetches the configured secrets when connecting, and applies them to the connect options.
    /// </summary>
    internal sealed class SecretsManagerPlugin : ITemporalClientPlugin
    {
        // Task.Delay doesn't accept longer delays
        private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(int.MaxValue);

        /// <summary>
        /// Initializes a new instance of the <see cref="SecretsManagerPlugin"/> class.
        /// </summary>
        /// <param name="ids">Configured secret IDs.</param>
        /// <param name="cache">Cache of the configured secrets.</param>
        public SecretsManagerPlugin(SecretIds ids, SecretCache cache)
        {
            Ids = ids;
            Cache = cache;
        }

        /// <inheritdoc />
        public string Name => "Temporalio.Extensions.Aws.SecretsManager";

        /// <summary>
        /// Gets the configured secret IDs.
        /// </summary>
        internal SecretIds Ids { get; }

        /// <summary>
        /// Gets the cache of the configured secrets.
        /// </summary>
        internal SecretCache Cache { get; }

        /// <summary>
        /// Gets or sets how often the API key is refreshed at most, and how long after a failed
        /// refresh it's retried. Checking more often would only be cache hits, and with a refresh
        /// interval of 0 (no caching), this bounds the fetch rate. Overridable in tests.
        /// </summary>
        internal TimeSpan MinRefreshInterval { get; set; } = SecretCache.FailedRefreshRetryInterval;

        /// <inheritdoc />
        public void ConfigureClient(TemporalClientOptions options)
        {
        }

        /// <inheritdoc />
        public async Task<TemporalConnection> ConnectAsync(
            TemporalClientConnectOptions options,
            Func<TemporalClientConnectOptions, Task<TemporalConnection>> continuation)
        {
            var runtime = options.Runtime ?? TemporalRuntime.Default;
            var tel = new Telemetry(
                runtime.MetricMeter,
                options.LoggerFactory.CreateLogger("Temporalio.Extensions.Aws.SecretsManager"));
            options = (TemporalClientConnectOptions)options.Clone();

            string? apiKey = null;
            if (Cache.ApiKey is { } apiKeyEntry)
            {
                if (options.ApiKey != null)
                {
                    throw new InvalidOperationException(
                        "cannot use API key from AWS Secrets Manager, API key already set");
                }
                // Like an API key from envconfig, this enables TLS unless it's explicitly
                // configured
                apiKey = await Cache.GetAsync(apiKeyEntry, tel).ConfigureAwait(false);
                options.ApiKey = apiKey;
            }

            if (Ids.HasTls)
            {
                if (options.Tls?.Disabled == true)
                {
                    throw new InvalidOperationException(
                        "cannot use TLS config from AWS Secrets Manager, TLS is disabled");
                }
                var tls = (TlsOptions?)options.Tls?.Clone() ?? new();
                if (Cache.ClientCert is { } clientCertEntry)
                {
                    if (tls.ClientCert != null || tls.ClientPrivateKey != null)
                    {
                        throw new InvalidOperationException(
                            "cannot use TLS client cert from AWS Secrets Manager, client certificates already set");
                    }
                    var pair = await Cache.GetAsync(clientCertEntry, tel).ConfigureAwait(false);
                    tls.ClientCert = pair.Cert;
                    tls.ClientPrivateKey = pair.Key;
                }
                if (Cache.ServerCACert is { } serverCACertEntry)
                {
                    if (tls.ServerRootCACert != null)
                    {
                        throw new InvalidOperationException(
                            "cannot use TLS server CA cert from AWS Secrets Manager, server root CA cert already set");
                    }
                    tls.ServerRootCACert =
                        await Cache.GetAsync(serverCACertEntry, tel).ConfigureAwait(false);
                }
                options.Tls = tls;
            }

            var connection = await continuation(options).ConfigureAwait(false);
            if (apiKey != null && Cache.ApiKey is { } entry)
            {
                _ = Task.Run(() => RefreshApiKeyAsync(new(connection), entry, apiKey, tel));
            }
            return connection;
        }

        /// <summary>
        /// Keep a connection's API key up to date while it is alive. The connection is only held
        /// weakly, since it has no other way to signal that it's no longer used.
        /// </summary>
        private async Task RefreshApiKeyAsync(
            WeakReference<TemporalConnection> connectionRef,
            CacheEntry<string> entry,
            string? apiKey,
            Telemetry tel)
        {
            var interval = Cache.RefreshInterval > MinRefreshInterval ?
                Cache.RefreshInterval : MinRefreshInterval;
            while (true)
            {
                bool failing;
                lock (entry.Lock)
                {
                    failing = entry.FailedAt != null;
                }
                // Retry sooner if the last refresh failed or the key was removed
                var delay = failing || apiKey == null ? MinRefreshInterval : interval;
                await Task.Delay(delay < MaxDelay ? delay : MaxDelay).ConfigureAwait(false);
                // Stop before fetching for a connection that was collected while waiting
                if (!connectionRef.TryGetTarget(out _))
                {
                    return;
                }
                string? newKey;
                Exception? error = null;
                try
                {
                    newKey = await Cache.GetAsync(entry, tel).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Any failure means there's no usable key, see below
                catch (Exception e)
#pragma warning restore CA1031
                {
                    // Expected failures are already counted by the cache, and are only thrown when
                    // there's no value within the max staleness, so stop using the
                    // current key instead of using it indefinitely. Calls fail until a refresh
                    // succeeds. Anything else is a bug, but is still treated as having no usable
                    // key, rather than letting this loop die and keep the current key forever.
                    if (e is not (SecretFetchException or TimeoutException))
                    {
                        tel.Record(() => tel.Logger.LogError(
                            e, "Unexpected error refreshing client API key from AWS Secrets Manager"));
                    }
                    newKey = null;
                    error = e;
                }
                if (newKey == apiKey)
                {
                    continue;
                }
                if (!connectionRef.TryGetTarget(out var connection))
                {
                    return;
                }
                try
                {
                    connection.ApiKey = newKey;
                }
#pragma warning disable CA1031 // Retrying is unlikely to help, see below
                catch (Exception e)
#pragma warning restore CA1031
                {
                    // E.g. the connection never connected, like a lazy connection returned by a
                    // plugin after this one instead of connecting, so there's no key to update
                    tel.Record(() => tel.Logger.LogWarning(
                        e, "Stopped refreshing client API key from AWS Secrets Manager"));
                    return;
                }
                apiKey = newKey;
                if (newKey == null)
                {
                    tel.Record(() => tel.Logger.LogWarning(
                        "Removed client API key, no AWS Secrets Manager value within max staleness " +
                        "(max_staleness={MaxStaleness}, error={Error})",
                        Cache.MaxStaleness,
                        error?.Message));
                }
                else
                {
                    tel.Record(() => tel.Logger.LogDebug(
                        "Updated client API key from AWS Secrets Manager"));
                }
            }
        }
    }
}
