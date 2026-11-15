namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using System.Collections.Concurrent;
using Amazon.SecretsManager.Model;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Extensions.Aws.SecretsManager;
using Xunit;
using static Temporalio.Tests.Extensions.Aws.SecretsManager.TestHelpers;

public class SecretsManagerExtensionTests
{
    internal const string Profile = "[profile.default]\naddress = \"localhost:7233\"\n";
    internal const string Ext = "[profile.default.ext.aws_secrets_manager]\n";
    internal const string ExtTls = "[profile.default.ext.aws_secrets_manager.tls]\n";
    internal const string ApiKeyProfile = Profile + Ext + "api_key_id = \"id\"\n";

    private const string AllProfile =
        Profile + Ext + "api_key_id = \"api-key\"\n" + ExtTls +
        "client_cert_id = \"bundle\"\nclient_key_id = \"bundle\"\nserver_ca_cert_id = \"ca\"\n";

    [Fact]
    public void CreateClientPlugin_NoSecretIdsMeansNoPlugin()
    {
        Assert.Null(Load(Profile, new FakeSecrets()).Plugins);
        Assert.Null(Load(Profile + Ext, new FakeSecrets()).Plugins);
    }

    [Fact]
    public void CreateClientPlugin_SecretIdsAreLoadedFromTomlAndEnv()
    {
        var toml = Profile + Ext + "api_key_id = \"toml-api-key\"\n" + ExtTls +
            "client_cert_id = \"toml-cert\"\nclient_key_id = \"toml-key\"\nserver_ca_cert_id = \"toml-ca\"\n";
        Assert.Equal("toml-api-key,toml-cert,toml-key,toml-ca", Ids(Plugin(Load(toml, new FakeSecrets()))));

        // Env vars override the file, and set-but-empty env vars unset it
        var env = new Dictionary<string, string>
        {
            ["TEMPORAL_EXT_AWS_SECRETS_MANAGER_API_KEY_ID"] = "env-api-key",
            ["TEMPORAL_EXT_AWS_SECRETS_MANAGER_TLS_SERVER_CA_CERT_ID"] = string.Empty,
        };
        Assert.Equal("env-api-key,toml-cert,toml-key,", Ids(Plugin(Load(toml, new FakeSecrets(), env))));

        // Env vars are ignored when env loading is disabled
        Assert.Null(
            Load(
                Profile,
                new FakeSecrets(),
                new Dictionary<string, string> { ["TEMPORAL_EXT_AWS_SECRETS_MANAGER_API_KEY_ID"] = "id" },
                disableEnv: true).Plugins);
    }

    [Theory]
    [InlineData(Ext + "foo = \"x\"", "unknown key \"foo\" in ext.aws_secrets_manager")]
    [InlineData(ExtTls + "foo = \"x\"", "unknown key \"foo\" in ext.aws_secrets_manager.tls")]
    [InlineData(Ext + "tls = \"x\"", "ext.aws_secrets_manager.tls must be a table, got string")]
    [InlineData(Ext + "api_key_id = 1", "ext.aws_secrets_manager.api_key_id must be a string, got number")]
    [InlineData(
        ExtTls + "client_cert_id = []",
        "ext.aws_secrets_manager.tls.client_cert_id must be a string, got array")]
    [InlineData(
        "api_key = \"key\"\n" + Ext + "api_key_id = \"id\"",
        "invalid config: API key secret ID cannot be set with an API key")]
    [InlineData(
        "[profile.default.tls]\nclient_cert_data = \"x\"\nclient_key_data = \"y\"\n" + ExtTls +
            "client_cert_id = \"c\"\nclient_key_id = \"k\"",
        "invalid config: TLS client cert secret ID cannot be set with client cert path or data; " +
            "TLS client key secret ID cannot be set with client key path or data")]
    [InlineData(
        "[profile.default.tls]\nserver_ca_cert_data = \"x\"\n" + ExtTls + "server_ca_cert_id = \"ca\"",
        "invalid config: TLS server CA cert secret ID cannot be set with server CA cert path or data")]
    [InlineData(
        "[profile.default.tls]\ndisabled = true\n" + ExtTls + "server_ca_cert_id = \"ca\"",
        "invalid config: TLS secret IDs cannot be set when TLS is disabled")]
    [InlineData(
        ExtTls + "client_cert_id = \"c\"",
        "invalid config: TLS client cert and key secret IDs must be set together")]
    public void CreateClientPlugin_InvalidConfig(string toml, string message)
    {
        var e = Assert.ThrowsAny<Exception>(() => Load(Profile + toml + "\n", new FakeSecrets()));
        Assert.EndsWith(message, e.Message);
        Assert.Contains("aws_secrets_manager", e.Message);
    }

    [Fact]
    public void CreateClientPlugin_ApiKeySecretIsAllowedWithTlsDisabled() =>
        Assert.NotNull(Load(
            Profile + "[profile.default.tls]\ndisabled = true\n" + Ext + "api_key_id = \"id\"\n",
            new FakeSecrets()).Plugins);

    [Fact]
    public void CreateClientPlugin_RefreshIntervalAndMaxStaleness()
    {
        var cache = Plugin(Load(ApiKeyProfile, new FakeSecrets())).Cache;
        Assert.Equal(TimeSpan.FromMinutes(5), cache.RefreshInterval);
        Assert.Equal(TimeSpan.FromHours(24), cache.MaxStaleness);

        cache = Plugin(Load(
            ApiKeyProfile,
            new FakeSecrets(),
            options: new() { RefreshInterval = TimeSpan.FromMinutes(1), MaxStaleness = TimeSpan.Zero })).Cache;
        Assert.Equal(TimeSpan.FromMinutes(1), cache.RefreshInterval);
        Assert.Equal(TimeSpan.FromHours(24), cache.MaxStaleness);

        // Max staleness is at least the refresh interval
        cache = Plugin(Load(
            ApiKeyProfile,
            new FakeSecrets(),
            options: new() { RefreshInterval = TimeSpan.FromHours(2), MaxStaleness = TimeSpan.FromHours(1) })).Cache;
        Assert.Equal(TimeSpan.FromHours(2), cache.MaxStaleness);

        var e = Assert.ThrowsAny<Exception>(() => Load(
            ApiKeyProfile, new FakeSecrets(), options: new() { RefreshInterval = TimeSpan.FromSeconds(-1) }));
        Assert.EndsWith("RefreshInterval must not be negative", e.Message);

        // From SECRETS_MANAGER_TTL in the process env
        Assert.Equal(TimeSpan.Zero, TtlCache("0").RefreshInterval);
        Assert.Equal(TimeSpan.FromSeconds(600), TtlCache("600").RefreshInterval);
        // The option takes precedence
        Assert.Equal(
            TimeSpan.FromSeconds(1),
            Plugin(Load(
                ApiKeyProfile,
                new FakeSecrets(),
                options: new() { RefreshInterval = TimeSpan.FromSeconds(1) },
                ttl: "600")).Cache.RefreshInterval);
        foreach (var invalid in new[] { "-1", "1.5", "abc", "99999999999999999999" })
        {
            e = Assert.ThrowsAny<Exception>(() => TtlCache(invalid));
            Assert.EndsWith(
                $"invalid SECRETS_MANAGER_TTL \"{invalid}\", must be a non-negative whole number of seconds",
                e.Message);
        }
    }

    [Fact]
    public async Task ConnectAsync_AppliesSecrets()
    {
        var secrets = AllSecrets();
        var options = Load(AllProfile, secrets);
        options.Tls = new() { Domain = "sni" };
        var connectOptions = await ConnectWithFakeAsync(options);
        Assert.Equal("secret-key", connectOptions.ApiKey);
        Assert.Equal("sni", connectOptions.Tls!.Domain);
        Assert.Equal(TestCertA, System.Text.Encoding.ASCII.GetString(connectOptions.Tls.ClientCert!));
        Assert.Equal(TestKeyA, System.Text.Encoding.ASCII.GetString(connectOptions.Tls.ClientPrivateKey!));
        Assert.Equal(TestCertA, System.Text.Encoding.ASCII.GetString(connectOptions.Tls.ServerRootCACert!));
        // The caller's options aren't modified
        Assert.Null(options.ApiKey);
        Assert.Null(options.Tls.ClientCert);
    }

    [Fact]
    public async Task ConnectAsync_ApiKeySecretLeavesTlsUnset()
    {
        var connectOptions = await ConnectWithFakeAsync(Load(ApiKeyProfile, new FakeSecrets().Put("id", "k")));
        Assert.Equal("k", connectOptions.ApiKey);
        // Unset, so that the API key enables TLS by default
        Assert.Null(connectOptions.Tls);
    }

    [Theory]
    [InlineData("api_key", "cannot use API key from AWS Secrets Manager, API key already set")]
    [InlineData("disabled", "cannot use TLS config from AWS Secrets Manager, TLS is disabled")]
    [InlineData(
        "client_cert",
        "cannot use TLS client cert from AWS Secrets Manager, client certificates already set")]
    [InlineData(
        "server_ca_cert",
        "cannot use TLS server CA cert from AWS Secrets Manager, server root CA cert already set")]
    public async Task ConnectAsync_Conflicts(string conflict, string message)
    {
        var options = Load(AllProfile, AllSecrets());
        switch (conflict)
        {
            case "api_key":
                options.ApiKey = "key";
                break;
            case "disabled":
                options.Tls = new() { Disabled = true };
                break;
            case "client_cert":
                options.Tls = new() { ClientCert = new byte[1], ClientPrivateKey = new byte[1] };
                break;
            case "server_ca_cert":
                options.Tls = new() { ServerRootCACert = new byte[1] };
                break;
        }
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => ConnectWithFakeAsync(options));
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public async Task ConnectAsync_FailsIfAFetchFails()
    {
        var e = await Assert.ThrowsAsync<SecretFetchException>(
            () => ConnectWithFakeAsync(Load(ApiKeyProfile, new FakeSecrets())));
        Assert.StartsWith("failed fetching AWS Secrets Manager secret \"id\": no secret id", e.Message);
    }

    internal static TemporalClientConnectOptions Load(
        string toml,
        FakeSecrets secrets,
        IReadOnlyDictionary<string, string>? env = null,
        bool disableEnv = false,
        SecretsManagerExtensionOptions? options = null,
        string? ttl = null)
    {
        options ??= new();
        options.SecretsClient = secrets.Client;
        var extension = new SecretsManagerExtension(options)
        {
            GetEnvironmentVariable = name => name == "SECRETS_MANAGER_TTL" ? ttl : null,
        };
        return ClientEnvConfig.LoadClientConnectOptions(new()
        {
            ConfigSource = DataSource.FromUTF8String(toml),
            OverrideEnvVars = env ?? new Dictionary<string, string>(),
            DisableEnv = disableEnv,
            Extensions = new[] { extension },
        });
    }

    internal static SecretsManagerPlugin Plugin(TemporalClientConnectOptions options) =>
        Assert.IsType<SecretsManagerPlugin>(Assert.Single(options.Plugins!));

    internal static FakeSecrets AllSecrets() => new FakeSecrets()
        .Put("api-key", "secret-key")
        .Put("bundle", TestCertA + TestKeyA)
        .Put("ca", TestCertA);

    private static string Ids(SecretsManagerPlugin plugin) =>
        string.Join(",", plugin.Ids.ApiKey, plugin.Ids.ClientCert, plugin.Ids.ClientKey, plugin.Ids.ServerCACert);

    private static SecretCache TtlCache(string ttl) =>
        Plugin(Load(ApiKeyProfile, new FakeSecrets(), ttl: ttl)).Cache;

    /// <summary>
    /// Runs the plugin's connect hook, returning the options it passes on.
    /// </summary>
    private static async Task<TemporalClientConnectOptions> ConnectWithFakeAsync(
        TemporalClientConnectOptions options)
    {
        TemporalClientConnectOptions? passed = null;
        await Plugin(options).ConnectAsync(options, o =>
        {
            passed = o;
            return Task.FromResult(TemporalConnection.CreateLazy(o));
        });
        return passed!;
    }

    /// <summary>
    /// A fake Secrets Manager client that returns <see cref="Secrets" />, counting fetches of each.
    /// </summary>
    internal sealed class FakeSecrets
    {
        private int version;

        public FakeSecrets() => Client = new(req =>
        {
            Calls.AddOrUpdate(req.SecretId, 1, (_, n) => n + 1);
            if (!Secrets.TryGetValue(req.SecretId, out var value))
            {
                throw new ResourceNotFoundException($"no secret {req.SecretId}")
                {
                    ErrorCode = "ResourceNotFoundException",
                };
            }
            if (value is Exception e)
            {
                throw e;
            }
            return new()
            {
                SecretString = (string)value,
                VersionId = $"v{Interlocked.Increment(ref version)}",
            };
        });

        public FakeSecretsManagerClient Client { get; }

        public ConcurrentDictionary<string, object> Secrets { get; } = new();

        public ConcurrentDictionary<string, int> Calls { get; } = new();

        public FakeSecrets Put(string id, object value)
        {
            Secrets[id] = value;
            return this;
        }

        public int CallCount(string id) => Calls.TryGetValue(id, out var n) ? n : 0;
    }
}
