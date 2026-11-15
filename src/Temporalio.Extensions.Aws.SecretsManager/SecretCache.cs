using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if NET5_0_OR_GREATER
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#endif
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// TTL cache of the configured credentials that falls back to the last fetched value, up to
    /// the max staleness old, when a refresh fails.
    /// </summary>
    /// <remarks>
    /// Fetches run on the thread pool independently of callers, so that they aren't affected by a
    /// caller timing out, and can be shared by clients on different connections.
    /// </remarks>
    internal sealed class SecretCache
    {
        /// <summary>
        /// After a failed refresh falls back to a stale value, wait this long before trying again
        /// so that an outage doesn't add a fetch attempt to every credential lookup.
        /// </summary>
        public static readonly TimeSpan FailedRefreshRetryInterval = TimeSpan.FromSeconds(30);

        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static readonly Regex CertPemRegex = new(
            "-----BEGIN CERTIFICATE-----\r?\n.*?-----END CERTIFICATE-----", RegexOptions.Singleline);

        private static readonly Regex KeyPemRegex = new(
            "-----BEGIN ((?:RSA |EC )?PRIVATE KEY)-----\r?\n.*?-----END \\1-----",
            RegexOptions.Singleline);

        private readonly IFetcher fetcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecretCache"/> class.
        /// </summary>
        /// <param name="fetcher">Fetcher.</param>
        /// <param name="ids">Configured secret IDs.</param>
        /// <param name="refreshInterval">How long a fetched secret is used before it's refreshed.</param>
        /// <param name="maxStaleness">How long a secret may still be used if refreshing it fails.</param>
        public SecretCache(
            IFetcher fetcher, SecretIds ids, TimeSpan refreshInterval, TimeSpan maxStaleness)
        {
            this.fetcher = fetcher;
            RefreshInterval = refreshInterval;
            MaxStaleness = maxStaleness;
            if (ids.ApiKey.Length > 0)
            {
                ApiKey = new(
                    new[] { new SecretRef(ids.ApiKey, Telemetry.SecretTypeApiKey) }, ParseApiKey);
            }
            if (ids.ClientCert.Length > 0)
            {
                ClientCert = NewClientCertEntry(ids.ClientCert, ids.ClientKey);
            }
            if (ids.ServerCACert.Length > 0)
            {
                ServerCACert = NewServerCACertEntry(ids.ServerCACert);
            }
        }

        /// <summary>
        /// Gets how long a fetched secret is used before it's refreshed.
        /// </summary>
        public TimeSpan RefreshInterval { get; }

        /// <summary>
        /// Gets how long a secret may still be used if refreshing it fails.
        /// </summary>
        public TimeSpan MaxStaleness { get; }

        /// <summary>
        /// Gets or sets how long callers wait for a fetch before falling back or failing.
        /// </summary>
        public TimeSpan FetchTimeout { get; set; } = Fetchers.FetchTimeout;

        /// <summary>
        /// Gets or sets the monotonic clock, overridable in tests.
        /// </summary>
        public Func<TimeSpan> Now { get; set; } = () => Clock.Elapsed;

        /// <summary>
        /// Gets the API key, if configured.
        /// </summary>
        public CacheEntry<string>? ApiKey { get; }

        /// <summary>
        /// Gets the client cert and key, fetched and replaced together to prevent tearing on
        /// rotations.
        /// </summary>
        public CacheEntry<ClientCertPair>? ClientCert { get; }

        /// <summary>
        /// Gets the server CA cert, as PEM, if configured.
        /// </summary>
        public CacheEntry<byte[]>? ServerCACert { get; }

        /// <summary>
        /// Get an entry's value, fetching it if needed. Returned values are shared across callers
        /// and must not be mutated.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="entry">Entry.</param>
        /// <param name="tel">Telemetry to record to.</param>
        /// <returns>The value.</returns>
        /// <exception cref="SecretFetchException">The fetch failed and there's no usable
        /// previous value.</exception>
        /// <exception cref="TimeoutException">The fetch timed out and there's no usable previous
        /// value.</exception>
        public async Task<T> GetAsync<T>(CacheEntry<T> entry, Telemetry tel)
        {
            TimeSpan start;
            bool canUseStale;
            T? cached;
            Task<T>? fetch = null;
            string? result = null;
            lock (entry.Lock)
            {
                start = Now();
                var hasValue = entry.VersionIds != null;
                canUseStale = hasValue && start - entry.FetchedAt < MaxStaleness;
                cached = entry.Value;
                if ((hasValue && start - entry.FetchedAt < RefreshInterval) ||
                    // Another caller is refreshing, so the current value is still the latest
                    // known one
                    (canUseStale && entry.InFlight != null))
                {
                    result = "hit";
                }
                else if (canUseStale && entry.FailedAt is { } failedAt &&
                    start - failedAt < FailedRefreshRetryInterval)
                {
                    result = "miss_with_fallback";
                }
                else
                {
                    // Wait for a fetch, starting one if none is in progress
                    fetch = entry.InFlight ??= StartRefreshAsync(entry, start, canUseStale, tel);
                }
            }
            if (result != null)
            {
                RecordLookup(entry, tel, result);
                return cached!;
            }

            var timeout = Task.Delay(FetchTimeout);
            if (await Task.WhenAny(fetch!, timeout).ConfigureAwait(false) == timeout)
            {
                if (canUseStale)
                {
                    RecordLookup(entry, tel, "miss_with_fallback");
                    return cached!;
                }
                throw new TimeoutException(
                    $"timed out after {FetchTimeout} waiting for AWS Secrets Manager secret fetch");
            }
            T value;
            try
            {
#pragma warning disable VSTHRD003 // Our own fetch task, which runs independently of callers
                value = await fetch!.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
            catch (SecretFetchException) when (canUseStale)
            {
                RecordLookup(entry, tel, "miss_with_fallback");
                return cached!;
            }
            RecordLookup(entry, tel, "miss");
            return value;
        }

        private static string ParseApiKey(IReadOnlyList<SecretValue> secrets)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(secrets[0].Value);
            }
            catch (ArgumentException)
            {
                throw new InvalidResponseException("API key secret is not valid UTF-8");
            }
        }

        private static string PemCerts(string pem) =>
            string.Concat(CertPemRegex.Matches(pem).Cast<Match>().Select(m => m.Value + "\n"));

        /// <summary>
        /// Entry for the client cert and key, which may be the same secret if bundled. Each
        /// secret's relevant PEM blocks are extracted, so a bundled secret can be used as both. On
        /// .NET 5+, a newly fetched pair that doesn't match (e.g. fetched partway through rotating
        /// separate cert and key secrets) is treated as a failed fetch, so the previous pair keeps
        /// being used.
        /// </summary>
        private static CacheEntry<ClientCertPair> NewClientCertEntry(string certId, string keyId)
        {
            var refs = new List<SecretRef> { new(certId, Telemetry.SecretTypeTlsClientCert) };
            if (keyId != certId)
            {
                refs.Add(new(keyId, Telemetry.SecretTypeTlsClientKey));
            }
            return new(refs, secrets =>
            {
                var certs = PemCerts(Encoding.UTF8.GetString(secrets[0].Value));
                if (certs.Length == 0)
                {
                    throw new InvalidResponseException(
                        $"no PEM certificate found in TLS client cert secret \"{certId}\"");
                }
                var keyMatch = KeyPemRegex.Match(Encoding.UTF8.GetString(secrets[secrets.Count - 1].Value));
                if (!keyMatch.Success)
                {
                    throw new InvalidResponseException(
                        $"no PEM private key found in TLS client key secret \"{keyId}\"");
                }
                var key = keyMatch.Value + "\n";
#if NET5_0_OR_GREATER
                try
                {
                    // Fails if the key doesn't match the cert
                    using var cert = X509Certificate2.CreateFromPem(certs, key);
                }
                catch (Exception e) when (e is CryptographicException or ArgumentException)
                {
                    throw new InvalidResponseException(
                        $"failed loading TLS client cert/key from AWS Secrets Manager secrets \"{certId}\" and \"{keyId}\": {e.Message}",
                        e);
                }
#endif
                return new(Encoding.ASCII.GetBytes(certs), Encoding.ASCII.GetBytes(key));
            });
        }

        private static CacheEntry<byte[]> NewServerCACertEntry(string secretId) =>
            new(new[] { new SecretRef(secretId, Telemetry.SecretTypeTlsServerCACert) }, secrets =>
            {
                var certs = PemCerts(Encoding.UTF8.GetString(secrets[0].Value));
                if (certs.Length == 0)
                {
                    throw new InvalidResponseException(
                        $"no PEM certificates found in TLS server CA cert secret \"{secretId}\"");
                }
                return Encoding.ASCII.GetBytes(certs);
            });

        private Task<T> StartRefreshAsync<T>(
            CacheEntry<T> entry, TimeSpan start, bool canUseStale, Telemetry tel)
        {
            var task = Task.Run(() => RefreshAsync(entry, start, canUseStale, tel));
            // Mark failures as observed so that UnobservedTaskException isn't raised when no caller
            // awaits the task, e.g. when callers stopped waiting and used the cached value. Failures
            // are already logged and counted.
            _ = task.ContinueWith(
                t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }

        /// <summary>
        /// Fetch an entry's secrets and record the outcome in the cache.
        /// </summary>
        private async Task<T> RefreshAsync<T>(
            CacheEntry<T> entry, TimeSpan start, bool canUseStale, Telemetry tel)
        {
            T value = default!;
            IReadOnlyList<string>? versionIds = null;
            SecretFetchException? failure = null;
            try
            {
                var secrets = new List<SecretValue>(entry.Refs.Count);
                foreach (var secretRef in entry.Refs)
                {
                    try
                    {
                        secrets.Add(await fetcher.FetchAsync(secretRef.Id).ConfigureAwait(false));
                    }
#pragma warning disable CA1031 // Every fetch error is recorded as a failed fetch
                    catch (Exception e)
#pragma warning restore CA1031
                    {
                        throw new SecretFetchException(
                            $"failed fetching AWS Secrets Manager secret \"{secretRef.Id}\": {e.Message}",
                            Telemetry.ClassifyFailure(e),
                            e);
                    }
                }
                try
                {
                    value = entry.Parse(secrets);
                }
                catch (InvalidResponseException e)
                {
                    throw new SecretFetchException(e.Message, "invalid_response", e);
                }
                versionIds = secrets.Select(s => s.VersionId).ToList();
            }
            catch (SecretFetchException e)
            {
                failure = e;
            }
#pragma warning disable CA1031 // Shouldn't happen, but don't leave callers without a result
            catch (Exception e)
#pragma warning restore CA1031
            {
                failure = new(e.Message, "other", e);
            }
            var latency = Now() - start;

            IReadOnlyList<string>? prevVersionIds;
            TimeSpan prevFetchedAt;
            bool prevFailed;
            lock (entry.Lock)
            {
                prevVersionIds = entry.VersionIds;
                prevFetchedAt = entry.FetchedAt;
                prevFailed = entry.FailedAt != null;
                if (failure != null)
                {
                    entry.FailedAt = start;
                }
                else
                {
                    entry.Value = value;
                    entry.VersionIds = versionIds;
                    entry.FetchedAt = start;
                    entry.FailedAt = null;
                }
                entry.InFlight = null;
            }

            try
            {
                if (failure != null)
                {
                    RecordFailure(entry, failure, latency, start - prevFetchedAt, canUseStale, tel);
                }
                else
                {
                    RecordSuccess(entry, versionIds!, prevVersionIds, prevFailed, latency, tel);
                }
            }
#pragma warning disable CA1031 // Telemetry failures shouldn't affect the fetch's result
            catch (Exception e)
#pragma warning restore CA1031
            {
                tel.Logger.LogWarning(e, "Failed recording AWS Secrets Manager telemetry");
            }
            if (failure != null)
            {
                throw failure;
            }
            return value;
        }

        private Dictionary<string, object> BaseTags<T>(CacheEntry<T> entry) => new()
        {
            [Telemetry.LabelSecretType] = entry.Refs[0].SecretType,
            [Telemetry.LabelFetchPath] = fetcher.Path,
        };

        private void RecordLookup<T>(CacheEntry<T> entry, Telemetry tel, string result)
        {
            var tags = BaseTags(entry);
            tags[Telemetry.LabelResult] = result;
            tel.RecordCacheLookup(tags);
        }

        private void RecordFailure<T>(
            CacheEntry<T> entry,
            SecretFetchException err,
            TimeSpan latency,
            TimeSpan age,
            bool canUseStale,
            Telemetry tel)
        {
            var latencyTags = BaseTags(entry);
            latencyTags[Telemetry.LabelOutcome] = "failure";
            tel.RecordFetchLatency(latency, latencyTags);
            var failedTags = BaseTags(entry);
            failedTags[Telemetry.LabelFailureReason] = err.Reason;
            failedTags[Telemetry.LabelFallback] = canUseStale ? "stale" : "none";
            tel.RecordFetchFailed(failedTags);
            if (canUseStale)
            {
                foreach (var secretRef in entry.Refs)
                {
                    tel.Logger.LogWarning(
                        "Failed refreshing AWS Secrets Manager secret, using previously fetched value " +
                        "(secret_id={SecretId}, secret_type={SecretType}, error={Error}, age={Age}, max_staleness={MaxStaleness})",
                        secretRef.Id,
                        secretRef.SecretType,
                        err.Message,
                        age,
                        MaxStaleness);
                }
            }
        }

        private void RecordSuccess<T>(
            CacheEntry<T> entry,
            IReadOnlyList<string> versionIds,
            IReadOnlyList<string>? prevVersionIds,
            bool prevFailed,
            TimeSpan latency,
            Telemetry tel)
        {
            var tags = BaseTags(entry);
            tags[Telemetry.LabelOutcome] = "success";
            tel.RecordFetchLatency(latency, tags);
            for (var i = 0; i < entry.Refs.Count; i++)
            {
                var secretRef = entry.Refs[i];
                if (prevVersionIds == null)
                {
                    tel.Logger.LogInformation(
                        "Fetched AWS Secrets Manager secret " +
                        "(secret_id={SecretId}, secret_type={SecretType}, fetch_path={FetchPath}, version_id={VersionId})",
                        secretRef.Id,
                        secretRef.SecretType,
                        fetcher.Path,
                        versionIds[i]);
                }
                else if (prevFailed || versionIds[i] != prevVersionIds[i])
                {
                    tel.Logger.LogInformation(
                        "Fetched updated AWS Secrets Manager secret " +
                        "(secret_id={SecretId}, secret_type={SecretType}, previous_version_id={PreviousVersionId}, version_id={VersionId})",
                        secretRef.Id,
                        secretRef.SecretType,
                        prevVersionIds[i],
                        versionIds[i]);
                }
            }
        }
    }
}
