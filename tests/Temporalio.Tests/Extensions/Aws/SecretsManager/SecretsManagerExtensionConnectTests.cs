namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using Temporalio.Client;
using Temporalio.Extensions.Aws.SecretsManager;
using Xunit;
using Xunit.Abstractions;
using static Temporalio.Tests.Extensions.Aws.SecretsManager.SecretsManagerExtensionTests;
using static Temporalio.Tests.Extensions.Aws.SecretsManager.TestHelpers;

/// <summary>
/// Tests that connect to the local test server.
/// </summary>
public class SecretsManagerExtensionConnectTests : WorkflowEnvironmentTestBase
{
    public SecretsManagerExtensionConnectTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task ConnectAsync_ApiKeyIsRefreshedInBackground()
    {
        var secrets = new FakeSecrets().Put("id", "key1");
        var options = LocalOptions(secrets, refreshInterval: TimeSpan.Zero);
        Plugin(options).MinRefreshInterval = TimeSpan.FromMilliseconds(20);
        var client = await TemporalClient.ConnectAsync(options);
        Assert.Equal("key1", client.Connection.ApiKey);

        secrets.Put("id", "key2");
        await WaitUntilAsync(() => client.Connection.ApiKey == "key2");

        // A failed refresh keeps the current key
        secrets.Put("id", new InvalidOperationException("down"));
        var calls = secrets.CallCount("id");
        await WaitUntilAsync(() => secrets.CallCount("id") > calls);
        Assert.Equal("key2", client.Connection.ApiKey);
        GC.KeepAlive(client);
    }

    [Fact]
    public async Task ConnectAsync_RefreshStopsWhenConnectionIsCollected()
    {
        var secrets = new FakeSecrets().Put("id", "key1");
        var options = LocalOptions(secrets, refreshInterval: TimeSpan.Zero);
        Plugin(options).MinRefreshInterval = TimeSpan.FromMilliseconds(20);
        var connectionRef = await ConnectAndForgetAsync(options);
        await WaitUntilAsync(() =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return !connectionRef.TryGetTarget(out _);
        });
        var calls = secrets.CallCount("id");
        await Task.Delay(200);
        // At most a refresh that was already in progress, rather than one every 20ms
        Assert.True(secrets.CallCount("id") <= calls + 1);
    }

    private static async Task<WeakReference<TemporalConnection>> ConnectAndForgetAsync(
        TemporalClientConnectOptions options)
    {
        var client = await TemporalClient.ConnectAsync(options);
        return new((TemporalConnection)client.Connection);
    }

    private TemporalClientConnectOptions LocalOptions(FakeSecrets secrets, TimeSpan refreshInterval)
    {
        var options = Load(
            $"[profile.default]\naddress = \"{Client.Connection.Options.TargetHost}\"\n" +
                "[profile.default.tls]\ndisabled = true\n" + Ext + "api_key_id = \"id\"\n",
            secrets,
            options: new SecretsManagerExtensionOptions { RefreshInterval = refreshInterval });
        options.Namespace = Client.Options.Namespace;
        return options;
    }
}
