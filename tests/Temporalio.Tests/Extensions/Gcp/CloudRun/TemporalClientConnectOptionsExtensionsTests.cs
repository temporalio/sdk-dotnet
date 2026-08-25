namespace Temporalio.Tests.Extensions.Gcp.CloudRun;

using Temporalio.Client;
using Temporalio.Extensions.Gcp.CloudRun;
using Temporalio.Tests.Extensions.OpenTelemetry;
using Xunit;

[Collection(OpenTelemetryEnvironmentDefinition.Name)]
public class TemporalClientConnectOptionsExtensionsTests
{
    private const string WorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
    private const string ServiceEnvironmentVariable = "K_SERVICE";
    private const string WorkerPoolRevisionEnvironmentVariable = "CLOUD_RUN_REVISION";
    private const string ServiceRevisionEnvironmentVariable = "K_REVISION";

    [Fact]
    public async Task ApplyGoogleCloudRunDefaultsAsync_SetsIdentityWhenUnset()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(revision: "revision-1");
        var options = new TemporalClientConnectOptions();

        var metadata = await options.ApplyGoogleCloudRunDefaultsAsync(
            server.Uri, TimeSpan.FromSeconds(5));

        Assert.Equal("instance-1@revision-1", metadata.WorkerIdentity);
        Assert.Equal("instance-1@revision-1", options.Identity);
    }

    [Fact]
    public async Task ApplyGoogleCloudRunDefaultsAsync_DoesNotOverrideExplicitIdentity()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(revision: "revision-1");
        var options = new TemporalClientConnectOptions { Identity = "custom-identity" };

        var metadata = await options.ApplyGoogleCloudRunDefaultsAsync(
            server.Uri, TimeSpan.FromSeconds(5));

        Assert.Equal("custom-identity", options.Identity);
        Assert.Equal("instance-1@revision-1", metadata.WorkerIdentity);
    }

    [Fact]
    public async Task ApplyGoogleCloudRunDefaultsAsync_NullOptionsThrows()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => TemporalClientConnectOptionsExtensions.ApplyGoogleCloudRunDefaultsAsync(null!));
    }

    private EnvironmentScope CloudRunEnvironment(string? revision) =>
        new(
            KeyValuePair.Create<string, string?>(WorkerPoolEnvironmentVariable, "pool-name"),
            KeyValuePair.Create<string, string?>(ServiceEnvironmentVariable, null),
            KeyValuePair.Create<string, string?>(WorkerPoolRevisionEnvironmentVariable, revision),
            KeyValuePair.Create<string, string?>(ServiceRevisionEnvironmentVariable, null));
}
