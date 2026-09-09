namespace Temporalio.Tests.Extensions.Gcp.CloudRun.WorkerId;

using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun.WorkerId;
using Temporalio.Tests.Extensions.OpenTelemetry;
using Xunit;

// Reuse the OpenTelemetry environment collection so tests that mutate the shared Cloud Run
// environment variables never run in parallel with each other or with the sibling extension tests
// that set the same variables.
[Collection(OpenTelemetryEnvironmentDefinition.Name)]
public class WorkerIdPluginTests
{
    private const string WorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
    private const string ServiceEnvironmentVariable = "K_SERVICE";
    private const string WorkerPoolRevisionEnvironmentVariable = "CLOUD_RUN_REVISION";
    private const string ServiceRevisionEnvironmentVariable = "K_REVISION";

    [Fact]
    public void Constructor_NullOptionsThrows() =>
        Assert.Throws<ArgumentNullException>(() => new WorkerIdPlugin(null!));

    [Fact]
    public async Task ConnectAsync_SetsIdentityWhenUnset()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(revision: "revision-1");
        var plugin = new WorkerIdPlugin(new WorkerIdPluginOptions
        {
            MetadataUri = server.Uri,
            Timeout = TimeSpan.FromSeconds(5),
        });
        var options = new TemporalClientConnectOptions();

        var continuationCalled = false;
        await plugin.ConnectAsync(
            options,
            connectOptions =>
            {
                continuationCalled = true;
                return Task.FromResult<TemporalConnection>(null!);
            });

        Assert.True(continuationCalled);
        Assert.Equal("instance-1@revision-1", options.Identity);
    }

    [Fact]
    public async Task ConnectAsync_DoesNotOverrideExplicitIdentity()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(revision: "revision-1");
        var plugin = new WorkerIdPlugin(new WorkerIdPluginOptions
        {
            MetadataUri = server.Uri,
            Timeout = TimeSpan.FromSeconds(5),
        });
        var options = new TemporalClientConnectOptions { Identity = "custom-identity" };

        await plugin.ConnectAsync(
            options, _ => Task.FromResult<TemporalConnection>(null!));

        Assert.Equal("custom-identity", options.Identity);
    }

    [Fact]
    public async Task ConnectAsync_ThrowsWhenNotOnCloudRun()
    {
        Uri uri;
        using (var server = new CloudRunMetadataServer())
        {
            uri = server.Uri;
        }

        // The server is disposed, so nothing is listening on that port anymore.
        var plugin = new WorkerIdPlugin(new WorkerIdPluginOptions
        {
            MetadataUri = uri,
            Timeout = TimeSpan.FromSeconds(5),
        });
        var options = new TemporalClientConnectOptions();

        var continuationCalled = false;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => plugin.ConnectAsync(
                options,
                _ =>
                {
                    continuationCalled = true;
                    return Task.FromResult<TemporalConnection>(null!);
                }));

        Assert.False(continuationCalled);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task ConnectAsync_CachesMetadataAcrossConnects()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(revision: "revision-1");
        var plugin = new WorkerIdPlugin(new WorkerIdPluginOptions
        {
            MetadataUri = server.Uri,
            Timeout = TimeSpan.FromSeconds(5),
        });

        var firstOptions = new TemporalClientConnectOptions();
        await plugin.ConnectAsync(
            firstOptions, _ => Task.FromResult<TemporalConnection>(null!));
        Assert.Equal("instance-1@revision-1", firstOptions.Identity);

        // A second connect reuses the metadata cached at the first connect (a single fetch), with
        // no second request to the metadata server.
        var secondOptions = new TemporalClientConnectOptions();
        await plugin.ConnectAsync(
            secondOptions, _ => Task.FromResult<TemporalConnection>(null!));
        Assert.Equal("instance-1@revision-1", secondOptions.Identity);

        Assert.Single(server.Requests);
    }

    private EnvironmentScope CloudRunEnvironment(string? revision) =>
        new(
            KeyValuePair.Create<string, string?>(WorkerPoolEnvironmentVariable, "pool-name"),
            KeyValuePair.Create<string, string?>(ServiceEnvironmentVariable, null),
            KeyValuePair.Create<string, string?>(WorkerPoolRevisionEnvironmentVariable, revision),
            KeyValuePair.Create<string, string?>(ServiceRevisionEnvironmentVariable, null));
}
