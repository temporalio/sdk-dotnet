using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

namespace Temporalio.Tests.Client;

public class TemporalClientTaskQueueTests : WorkflowEnvironmentTestBase
{
    public TemporalClientTaskQueueTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task BasicSeriesOfUpdates_Succeeds()
    {
        var taskQueue = Guid.NewGuid().ToString();

        await Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewDefault("1.0"));

        var sets = await Client.GetWorkerBuildIdCompatibilityAsync(taskQueue);
        Assert.Equal(1, sets.VersionSets.Count);
        Assert.Equal("1.0", sets.DefaultBuildId());

        await Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewCompatible("1.1", "1.0"));

        sets = await Client.GetWorkerBuildIdCompatibilityAsync(taskQueue);
        Assert.Equal(1, sets.VersionSets.Count);
        Assert.Equal("1.1", sets.DefaultBuildId());

        await Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.PromoteBuildIdWithinSet("1.0"));

        sets = await Client.GetWorkerBuildIdCompatibilityAsync(taskQueue);
        Assert.Equal(1, sets.VersionSets.Count);
        Assert.Equal("1.0", sets.DefaultBuildId());

        await Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.AddNewDefault("2.0"));

        sets = await Client.GetWorkerBuildIdCompatibilityAsync(taskQueue);
        Assert.Equal(2, sets.VersionSets.Count);
        Assert.Equal("2.0", sets.DefaultBuildId());

        await Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.PromoteSetByBuildId("1.0"));

        sets = await Client.GetWorkerBuildIdCompatibilityAsync(taskQueue);
        Assert.Equal(2, sets.VersionSets.Count);
        Assert.Equal("1.0", sets.DefaultBuildId());

        await Client.UpdateWorkerBuildIdCompatibilityAsync(taskQueue, new BuildIdOp.MergeSets("2.0", "1.0"));

        sets = await Client.GetWorkerBuildIdCompatibilityAsync(taskQueue);
        Assert.Equal(1, sets.VersionSets.Count);
        Assert.Equal("2.0", sets.DefaultBuildId());
    }
}