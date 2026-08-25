namespace Temporalio.Tests.Extensions.Gcp.CloudRun;

using Temporalio.Common;
using Temporalio.Extensions.Gcp.CloudRun;
using Temporalio.Worker;
using Xunit;

public class TemporalWorkerOptionsExtensionsTests
{
    [Fact]
    public void ApplyGoogleCloudRunDefaults_SetsPinnedDeploymentOptions()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", "revision-1");
        var options = new TemporalWorkerOptions("task-queue");

        var result = options.ApplyGoogleCloudRunDefaults(metadata);

        Assert.Same(options, result);
        var deployment = options.DeploymentOptions;
        Assert.NotNull(deployment);
        Assert.Equal(new WorkerDeploymentVersion("pool-name", "revision-1"), deployment!.Version);
        Assert.True(deployment.UseWorkerVersioning);
        Assert.Equal(VersioningBehavior.Pinned, deployment.DefaultVersioningBehavior);
    }

    [Fact]
    public void ApplyGoogleCloudRunDefaults_ThrowsWhenNameEmpty()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", string.Empty, "revision-1");
        var options = new TemporalWorkerOptions("task-queue");

        Assert.Throws<InvalidOperationException>(() => options.ApplyGoogleCloudRunDefaults(metadata));
    }

    [Fact]
    public void ApplyGoogleCloudRunDefaults_ThrowsWhenRevisionEmpty()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", string.Empty);
        var options = new TemporalWorkerOptions("task-queue");

        Assert.Throws<InvalidOperationException>(() => options.ApplyGoogleCloudRunDefaults(metadata));
    }

    [Fact]
    public void ApplyGoogleCloudRunDefaults_NullOptionsThrows()
    {
        var metadata = new GoogleCloudRunMetadata("instance-1", "pool-name", "revision-1");

        Assert.Throws<ArgumentNullException>(
            () => TemporalWorkerOptionsExtensions.ApplyGoogleCloudRunDefaults(null!, metadata));
    }

    [Fact]
    public void ApplyGoogleCloudRunDefaults_NullMetadataThrows()
    {
        var options = new TemporalWorkerOptions("task-queue");

        Assert.Throws<ArgumentNullException>(() => options.ApplyGoogleCloudRunDefaults(null!));
    }
}
