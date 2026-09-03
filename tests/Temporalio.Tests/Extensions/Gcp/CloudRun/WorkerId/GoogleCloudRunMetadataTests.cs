namespace Temporalio.Tests.Extensions.Gcp.CloudRun.WorkerId;

using Temporalio.Common;
using Temporalio.Extensions.Gcp.CloudRun.WorkerId;
using Temporalio.Tests.Extensions.OpenTelemetry;
using Xunit;

// Reuse the OpenTelemetry environment collection so tests that mutate the shared Cloud Run
// environment variables never run in parallel with each other or with the sibling
// OpenTelemetry extension tests that set the same variables.
[Collection(OpenTelemetryEnvironmentDefinition.Name)]
public class GoogleCloudRunMetadataTests
{
    private const string WorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
    private const string ServiceEnvironmentVariable = "K_SERVICE";
    private const string WorkerPoolRevisionEnvironmentVariable = "CLOUD_RUN_REVISION";
    private const string ServiceRevisionEnvironmentVariable = "K_REVISION";

    [Fact]
    public async Task FetchAsync_NameUsesWorkerPoolOverService()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(
            workerPool: "pool-name",
            service: "service-name",
            workerPoolRevision: null,
            serviceRevision: null);

        var metadata = await GoogleCloudRunMetadata.FetchAsync(server.Uri, TimeSpan.FromSeconds(5));

        Assert.Equal("pool-name", metadata.Name);
    }

    [Fact]
    public async Task FetchAsync_NameFallsBackToServiceWhenWorkerPoolUnset()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(
            workerPool: null,
            service: "service-name",
            workerPoolRevision: null,
            serviceRevision: null);

        var metadata = await GoogleCloudRunMetadata.FetchAsync(server.Uri, TimeSpan.FromSeconds(5));

        Assert.Equal("service-name", metadata.Name);
    }

    [Fact]
    public async Task FetchAsync_RevisionUsesWorkerPoolRevisionOverServiceRevision()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(
            workerPool: "pool-name",
            service: null,
            workerPoolRevision: "cloud-run-revision",
            serviceRevision: "k-revision");

        var metadata = await GoogleCloudRunMetadata.FetchAsync(server.Uri, TimeSpan.FromSeconds(5));

        Assert.Equal("cloud-run-revision", metadata.Revision);
    }

    [Fact]
    public async Task FetchAsync_RevisionFallsBackToServiceRevisionWhenWorkerPoolRevisionUnset()
    {
        using var server = new CloudRunMetadataServer(body: "instance-1");
        using var env = CloudRunEnvironment(
            workerPool: "pool-name",
            service: null,
            workerPoolRevision: null,
            serviceRevision: "k-revision");

        var metadata = await GoogleCloudRunMetadata.FetchAsync(server.Uri, TimeSpan.FromSeconds(5));

        Assert.Equal("k-revision", metadata.Revision);
    }

    [Fact]
    public async Task FetchAsync_SendsMetadataFlavorHeaderAndTrimsInstanceId()
    {
        using var server = new CloudRunMetadataServer(body: "  instance-1234\n");
        using var env = CloudRunEnvironment(
            workerPool: null,
            service: null,
            workerPoolRevision: null,
            serviceRevision: null);

        var metadata = await GoogleCloudRunMetadata.FetchAsync(server.Uri, TimeSpan.FromSeconds(5));

        Assert.Equal("instance-1234", metadata.InstanceId);
        var request = Assert.Single(server.Requests);
        Assert.Contains("Metadata-Flavor: Google", request);
    }

    [Fact]
    public async Task FetchAsync_ThrowsWhenMetadataServerReturnsError()
    {
        using var server = new CloudRunMetadataServer(
            statusCode: 500, reasonPhrase: "Internal Server Error");
        using var env = CloudRunEnvironment(
            workerPool: null,
            service: null,
            workerPoolRevision: null,
            serviceRevision: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GoogleCloudRunMetadata.FetchAsync(server.Uri, TimeSpan.FromSeconds(5)));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task FetchAsync_ThrowsWhenMetadataServerUnreachable()
    {
        Uri uri;
        using (var server = new CloudRunMetadataServer())
        {
            uri = server.Uri;
        }

        // The server is disposed, so nothing is listening on that port anymore.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => GoogleCloudRunMetadata.FetchAsync(uri, TimeSpan.FromSeconds(5)));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public void WorkerIdentity_UsesInstanceIdAndRevision()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", "revision-1");

        Assert.Equal("instance-1@revision-1", metadata.WorkerIdentity);
    }

    [Fact]
    public void WorkerIdentity_FallsBackToNameWhenRevisionEmpty()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", string.Empty);

        Assert.Equal("instance-1@pool-name", metadata.WorkerIdentity);
    }

    [Fact]
    public void WorkerIdentity_FallsBackToInstanceIdWhenNameAndRevisionEmpty()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", string.Empty, string.Empty);

        Assert.Equal("instance-1", metadata.WorkerIdentity);
    }

    [Fact]
    public void ToWorkerDeploymentVersion_UsesNameAndRevision()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", "revision-1");

        Assert.Equal(
            new WorkerDeploymentVersion("pool-name", "revision-1"),
            metadata.ToWorkerDeploymentVersion());
    }

    [Fact]
    public void ToWorkerDeploymentVersion_ThrowsWhenNameEmpty()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", string.Empty, "revision-1");

        Assert.Throws<InvalidOperationException>(() => metadata.ToWorkerDeploymentVersion());
    }

    [Fact]
    public void ToWorkerDeploymentVersion_ThrowsWhenRevisionEmpty()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", string.Empty);

        Assert.Throws<InvalidOperationException>(() => metadata.ToWorkerDeploymentVersion());
    }

    private EnvironmentScope CloudRunEnvironment(
        string? workerPool,
        string? service,
        string? workerPoolRevision,
        string? serviceRevision) =>
        new(
            KeyValuePair.Create(WorkerPoolEnvironmentVariable, workerPool),
            KeyValuePair.Create(ServiceEnvironmentVariable, service),
            KeyValuePair.Create(WorkerPoolRevisionEnvironmentVariable, workerPoolRevision),
            KeyValuePair.Create(ServiceRevisionEnvironmentVariable, serviceRevision));
}
