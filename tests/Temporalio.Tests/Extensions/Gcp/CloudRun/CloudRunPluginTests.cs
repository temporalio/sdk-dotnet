namespace Temporalio.Tests.Extensions.Gcp.CloudRun;

using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Extensions.Gcp.CloudRun;
using Temporalio.Tests.Extensions.OpenTelemetry;
using Temporalio.Worker;
using Xunit;

// Reuse the OpenTelemetry environment collection so tests that mutate the shared Cloud Run
// environment variables never run in parallel with each other or with the sibling extension tests
// that set the same variables.
[Collection(OpenTelemetryEnvironmentDefinition.Name)]
public class CloudRunPluginTests
{
    private const string WorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
    private const string ServiceEnvironmentVariable = "K_SERVICE";
    private const string WorkerPoolRevisionEnvironmentVariable = "CLOUD_RUN_REVISION";
    private const string ServiceRevisionEnvironmentVariable = "K_REVISION";

    [Fact]
    public void Constructor_NullOptionsThrows() =>
        Assert.Throws<ArgumentNullException>(() => new CloudRunPlugin(null!));

    [Fact]
    public async Task ConnectAsync_SetsIdentityWhenUnset()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(revision: "revision-1");
        var plugin = new CloudRunPlugin(new CloudRunPluginOptions
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
        var plugin = new CloudRunPlugin(new CloudRunPluginOptions
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
        var plugin = new CloudRunPlugin(new CloudRunPluginOptions
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
    public void ConfigureWorker_SetsPinnedDeploymentOptions()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", "revision-1");
        var plugin = new CloudRunPlugin(new CloudRunPluginOptions { Metadata = metadata });
        var options = new TemporalWorkerOptions("task-queue");

        plugin.ConfigureWorker(options);

        var deployment = options.DeploymentOptions;
        Assert.NotNull(deployment);
        Assert.Equal(new WorkerDeploymentVersion("pool-name", "revision-1"), deployment!.Version);
        Assert.True(deployment.UseWorkerVersioning);
        Assert.Equal(VersioningBehavior.Pinned, deployment.DefaultVersioningBehavior);
    }

    [Fact]
    public void ConfigureWorker_ThrowsWhenMetadataNotFetched()
    {
        var plugin = new CloudRunPlugin(new CloudRunPluginOptions());
        var options = new TemporalWorkerOptions("task-queue");

        Assert.Throws<InvalidOperationException>(() => plugin.ConfigureWorker(options));
    }

    [Fact]
    public async Task ConnectThenConfigureWorker_UsesCachedMetadata()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(revision: "revision-1");
        var plugin = new CloudRunPlugin(new CloudRunPluginOptions
        {
            MetadataUri = server.Uri,
            Timeout = TimeSpan.FromSeconds(5),
        });

        var connectOptions = new TemporalClientConnectOptions();
        await plugin.ConnectAsync(
            connectOptions, _ => Task.FromResult<TemporalConnection>(null!));
        Assert.Equal("instance-1@revision-1", connectOptions.Identity);

        // The worker hook reuses the metadata cached at connect time (a single fetch), with no
        // second request to the metadata server.
        var workerOptions = new TemporalWorkerOptions("task-queue");
        plugin.ConfigureWorker(workerOptions);

        var deployment = workerOptions.DeploymentOptions;
        Assert.NotNull(deployment);
        Assert.Equal(new WorkerDeploymentVersion("pool-name", "revision-1"), deployment!.Version);
        Assert.True(deployment.UseWorkerVersioning);
        Assert.Equal(VersioningBehavior.Pinned, deployment.DefaultVersioningBehavior);
        Assert.Single(server.Requests);
    }

    private EnvironmentScope CloudRunEnvironment(string? revision) =>
        new(
            KeyValuePair.Create<string, string?>(WorkerPoolEnvironmentVariable, "pool-name"),
            KeyValuePair.Create<string, string?>(ServiceEnvironmentVariable, null),
            KeyValuePair.Create<string, string?>(WorkerPoolRevisionEnvironmentVariable, revision),
            KeyValuePair.Create<string, string?>(ServiceRevisionEnvironmentVariable, null));
}
