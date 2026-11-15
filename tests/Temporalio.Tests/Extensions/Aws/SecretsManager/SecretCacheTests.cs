namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Temporalio.Extensions.Aws.SecretsManager;
using Xunit;
using static Temporalio.Tests.Extensions.Aws.SecretsManager.TestHelpers;

public class SecretCacheTests
{
    private static readonly TimeSpan Refresh = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxStaleness = TimeSpan.FromHours(1);
    private static readonly TimeSpan Retry = SecretCache.FailedRefreshRetryInterval;

    private readonly FakeFetcher fetcher = new();
    private readonly RecordingTelemetry tel = new();
    private TimeSpan now = TimeSpan.FromHours(1000);

    [Fact]
    public async Task GetAsync_HitMissAndRotation()
    {
        fetcher.Set("key", "v1", "ver1");
        var cache = NewApiKeyCache(Refresh);
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal(1, fetcher.Calls("key"));
        Assert.Equal(
            new[]
            {
                Tags("secret_type", "api_key", "fetch_path", "sdk", "result", "miss"),
                Tags("secret_type", "api_key", "fetch_path", "sdk", "result", "hit"),
            },
            tel.Take(Telemetry.MetricCacheLookup));
        Assert.Equal(
            new[] { Tags("secret_type", "api_key", "fetch_path", "sdk", "outcome", "success") },
            tel.Take(Telemetry.MetricFetchLatency));

        now += Refresh;
        fetcher.Set("key", "v2", "ver2");
        Assert.Equal("v2", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal(2, fetcher.Calls("key"));
    }

    [Fact]
    public async Task GetAsync_ZeroRefreshIntervalAlwaysFetches()
    {
        fetcher.Set("key", "v1", "ver1");
        var cache = NewApiKeyCache(TimeSpan.Zero);
        await cache.GetAsync(cache.ApiKey!, tel);
        await cache.GetAsync(cache.ApiKey!, tel);
        Assert.Equal(2, fetcher.Calls("key"));
    }

    [Fact]
    public async Task GetAsync_FailureFallsBackToStaleValueAndWaitsBeforeRetrying()
    {
        fetcher.Set("key", "v1", "ver1");
        var cache = NewApiKeyCache(Refresh);
        await cache.GetAsync(cache.ApiKey!, tel);
        tel.Take(Telemetry.MetricCacheLookup);

        fetcher.Fail("key", new AmazonSecretsManagerException("denied") { ErrorCode = "AccessDeniedException" });
        now += Refresh;
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal(
            new[]
            {
                Tags(
                    "secret_type",
                    "api_key",
                    "fetch_path",
                    "sdk",
                    "failure_reason",
                    "access_denied",
                    "fallback",
                    "stale"),
            },
            tel.Take(Telemetry.MetricFetchFailed));

        // Not retried until the retry interval has passed
        now += Retry - TimeSpan.FromTicks(1);
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal(2, fetcher.Calls("key"));
        now += TimeSpan.FromTicks(1);
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal(3, fetcher.Calls("key"));
        Assert.Equal(
            new[] { "miss_with_fallback", "miss_with_fallback", "miss_with_fallback" },
            Results());

        fetcher.Set("key", "v1", "ver1");
        now += Retry;
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Null(cache.ApiKey!.FailedAt);
    }

    [Fact]
    public async Task GetAsync_FailureWithoutPreviousValueThrows()
    {
        fetcher.Fail("key", new ResourceNotFoundException("nope") { ErrorCode = "ResourceNotFoundException" });
        var cache = NewApiKeyCache(Refresh);
        var e = await Assert.ThrowsAsync<SecretFetchException>(() => cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal("failed fetching AWS Secrets Manager secret \"key\": nope", e.Message);
        Assert.Equal("resource_not_found", e.Reason);
        Assert.Equal("none", tel.Take(Telemetry.MetricFetchFailed).Single()["fallback"]);
        Assert.Empty(Results());
    }

    [Fact]
    public async Task GetAsync_StaleValueIsNotUsedPastMaxStaleness()
    {
        fetcher.Set("key", "v1", "ver1");
        var cache = NewApiKeyCache(Refresh);
        await cache.GetAsync(cache.ApiKey!, tel);
        fetcher.Fail("key", new InvalidOperationException("down"));
        now += MaxStaleness;
        await Assert.ThrowsAsync<SecretFetchException>(() => cache.GetAsync(cache.ApiKey!, tel));
    }

    [Fact]
    public async Task GetAsync_ConcurrentCallsShareAFetch()
    {
        fetcher.Set("key", "v1", "ver1");
        fetcher.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = NewApiKeyCache(Refresh);
        var gets = new[] { cache.GetAsync(cache.ApiKey!, tel), cache.GetAsync(cache.ApiKey!, tel) };
        await WaitUntilAsync(() => fetcher.Calls("key") == 1);
        fetcher.Gate.SetResult();
        Assert.Equal(new[] { "v1", "v1" }, await Task.WhenAll(gets));
        Assert.Equal(1, fetcher.Calls("key"));
    }

    [Fact]
    public async Task GetAsync_RefreshingServesStaleValueToOtherCallers()
    {
        fetcher.Set("key", "v1", "ver1");
        var cache = NewApiKeyCache(Refresh);
        await cache.GetAsync(cache.ApiKey!, tel);

        fetcher.Set("key", "v2", "ver2");
        fetcher.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        now += Refresh;
        var refreshing = cache.GetAsync(cache.ApiKey!, tel);
        await WaitUntilAsync(() => fetcher.Calls("key") == 2);
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        fetcher.Gate.SetResult();
        Assert.Equal("v2", await refreshing);
    }

    [Fact]
    public async Task GetAsync_CallerTimingOutDoesNotCancelFetch()
    {
        fetcher.Set("key", "v1", "ver1");
        fetcher.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = NewApiKeyCache(Refresh);
        cache.FetchTimeout = TimeSpan.FromMilliseconds(10);
        var e = await Assert.ThrowsAsync<TimeoutException>(() => cache.GetAsync(cache.ApiKey!, tel));
        Assert.StartsWith("timed out after", e.Message);

        fetcher.Gate.SetResult();
        await WaitUntilAsync(() => InFlight(cache.ApiKey!) == null);
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal(1, fetcher.Calls("key"));
    }

    [Fact]
    public async Task GetAsync_TimedOutWaitFallsBackToStaleValue()
    {
        fetcher.Set("key", "v1", "ver1");
        var cache = NewApiKeyCache(TimeSpan.Zero);
        await cache.GetAsync(cache.ApiKey!, tel);
        tel.Take(Telemetry.MetricCacheLookup);

        fetcher.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.FetchTimeout = TimeSpan.FromMilliseconds(10);
        now += TimeSpan.FromTicks(1);
        Assert.Equal("v1", await cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal(new[] { "miss_with_fallback" }, Results());
        fetcher.Gate.SetResult();
        await WaitUntilAsync(() => InFlight(cache.ApiKey!) == null);
    }

    [Fact]
    public async Task GetAsync_BundledClientCertAndKey()
    {
        fetcher.Set("bundle", "junk\n" + TestCertA + TestKeyA, "ver1");
        var cache = NewCache(new(clientCert: "bundle", clientKey: "bundle"), Refresh);
        var pair = await cache.GetAsync(cache.ClientCert!, tel);
        Assert.Equal(TestCertA, System.Text.Encoding.ASCII.GetString(pair.Cert));
        Assert.Equal(TestKeyA, System.Text.Encoding.ASCII.GetString(pair.Key));
        Assert.Equal(1, fetcher.Calls("bundle"));
    }

    [Fact]
    public async Task GetAsync_SeparateClientCertAndKey()
    {
        fetcher.Set("cert", TestCertA, "ver1");
        // Traditional EC keys are passed through to Core, which accepts them
        fetcher.Set("key", TestKeyAEc, "ver2");
        var cache = NewCache(new(clientCert: "cert", clientKey: "key"), Refresh);
        var pair = await cache.GetAsync(cache.ClientCert!, tel);
        Assert.Equal(TestKeyAEc, System.Text.Encoding.ASCII.GetString(pair.Key));
        // Both are reported as the client cert in metrics
        Assert.Equal(
            new[] { Tags("secret_type", "tls_client_cert", "fetch_path", "sdk", "result", "miss") },
            tel.Take(Telemetry.MetricCacheLookup));
    }

    [Fact]
    public async Task GetAsync_MismatchedClientCertAndKeyKeepThePreviousPair()
    {
        fetcher.Set("cert", TestCertA, "ver1");
        fetcher.Set("key", TestKeyA, "ver1");
        var cache = NewCache(new(clientCert: "cert", clientKey: "key"), Refresh);
        await cache.GetAsync(cache.ClientCert!, tel);

        // Fetched partway through rotating the separate secrets
        fetcher.Set("cert", TestCertB, "ver2");
        now += Refresh;
        var pair = await cache.GetAsync(cache.ClientCert!, tel);
        Assert.Equal(TestCertA, System.Text.Encoding.ASCII.GetString(pair.Cert));
        Assert.Equal(
            "invalid_response",
            tel.Take(Telemetry.MetricFetchFailed).Single()["failure_reason"]);

        fetcher.Set("key", TestKeyB, "ver2");
        now += Retry;
        pair = await cache.GetAsync(cache.ClientCert!, tel);
        Assert.Equal(TestCertB, System.Text.Encoding.ASCII.GetString(pair.Cert));
    }

    [Theory]
    [InlineData("no cert", "no PEM certificate found in TLS client cert secret \"cert\"")]
    [InlineData("no key", "no PEM private key found in TLS client key secret \"key\"")]
    [InlineData(
        "mismatched",
        "failed loading TLS client cert/key from AWS Secrets Manager secrets \"cert\" and \"key\": ")]
    public async Task GetAsync_InvalidClientCertAndKey(string invalidCase, string message)
    {
        var (cert, key) = invalidCase switch
        {
            "no cert" => (TestKeyA, TestKeyA),
            "no key" => (TestCertA, TestCertA),
            _ => (TestCertA, TestKeyB),
        };
        fetcher.Set("cert", cert, "ver1");
        fetcher.Set("key", key, "ver1");
        var cache = NewCache(new(clientCert: "cert", clientKey: "key"), Refresh);
        var e = await Assert.ThrowsAsync<SecretFetchException>(
            () => cache.GetAsync(cache.ClientCert!, tel));
        Assert.StartsWith(message, e.Message);
        Assert.Equal("invalid_response", e.Reason);
    }

    [Fact]
    public async Task GetAsync_InvalidUtf8ApiKey()
    {
        fetcher.Set("key", new byte[] { 0xff, 0xfe }, "ver1");
        var cache = NewApiKeyCache(Refresh);
        var e = await Assert.ThrowsAsync<SecretFetchException>(() => cache.GetAsync(cache.ApiKey!, tel));
        Assert.Equal("API key secret is not valid UTF-8", e.Message);
        Assert.Equal("invalid_response", e.Reason);
    }

    [Fact]
    public async Task GetAsync_ServerCACert()
    {
        fetcher.Set("ca", TestCertA + "\n" + TestCertB, "ver1");
        var cache = NewCache(new(serverCACert: "ca"), Refresh);
        Assert.Equal(
            TestCertA + TestCertB,
            System.Text.Encoding.ASCII.GetString(await cache.GetAsync(cache.ServerCACert!, tel)));

        fetcher.Set("ca2", "nothing here", "ver1");
        var cache2 = NewCache(new(serverCACert: "ca2"), Refresh);
        var e = await Assert.ThrowsAsync<SecretFetchException>(
            () => cache2.GetAsync(cache2.ServerCACert!, tel));
        Assert.Equal("no PEM certificates found in TLS server CA cert secret \"ca2\"", e.Message);
    }

    private static Dictionary<string, object> Tags(params string[] kv)
    {
        var tags = new Dictionary<string, object>();
        for (var i = 0; i < kv.Length; i += 2)
        {
            tags[kv[i]] = kv[i + 1];
        }
        return tags;
    }

    private static Task<T>? InFlight<T>(CacheEntry<T> entry)
    {
        lock (entry.Lock)
        {
            return entry.InFlight;
        }
    }

    private SecretCache NewCache(SecretIds ids, TimeSpan refresh) =>
        new(fetcher, ids, refresh, MaxStaleness) { Now = () => now };

    private SecretCache NewApiKeyCache(TimeSpan refresh) => NewCache(new(apiKey: "key"), refresh);

    private List<object> Results() =>
        tel.Take(Telemetry.MetricCacheLookup).Select(t => t["result"]).ToList();
}
