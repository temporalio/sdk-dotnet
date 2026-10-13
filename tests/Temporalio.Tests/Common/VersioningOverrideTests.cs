namespace Temporalio.Tests.Common;

using Temporalio.Common;
using Xunit;
using ApiWorkflow = Temporalio.Api.Workflow.V1;
using ProtoExtensions = Temporalio.Workflows.ProtoExtensions;

public class VersioningOverrideTests
{
    [Theory]
    [InlineData(VersioningOverride.PinnedOverrideBehavior.Pinned)]
    [InlineData(VersioningOverride.PinnedOverrideBehavior.Unspecified)]
    public void ToProto_Pinned_PreservesLegacyFieldsAndBehavior(
        VersioningOverride.PinnedOverrideBehavior behavior)
    {
        var version = new WorkerDeploymentVersion("deployment", "build.id");
        var value = new VersioningOverride.Pinned(version, behavior);
        var proto = value.ToProto();
        Assert.Equal(ApiWorkflow.VersioningOverride.OverrideOneofCase.Pinned, proto.OverrideCase);
        Assert.Equal(version.ToProto(), proto.Pinned.Version);
        Assert.Equal(
            (ApiWorkflow.VersioningOverride.Types.PinnedOverrideBehavior)behavior,
            proto.Pinned.Behavior);
#pragma warning disable CS0612
        Assert.Equal(Api.Enums.V1.VersioningBehavior.Pinned, proto.Behavior);
        Assert.Equal("deployment.build.id", proto.PinnedVersion);
#pragma warning restore CS0612
        Assert.Equal(proto, ProtoExtensions.ToProto(value));
        Assert.Equal(value, ProtoExtensions.FromVersioningOverrideProto(proto));
    }

    [Fact]
    public void ToProto_AutoUpgrade_PreservesLegacyBehavior()
    {
        var value = new VersioningOverride.AutoUpgrade();
        var proto = value.ToProto();
        Assert.Equal(ApiWorkflow.VersioningOverride.OverrideOneofCase.AutoUpgrade, proto.OverrideCase);
        Assert.True(proto.AutoUpgrade);
#pragma warning disable CS0612
        Assert.Equal(Api.Enums.V1.VersioningBehavior.AutoUpgrade, proto.Behavior);
        Assert.Empty(proto.PinnedVersion);
        Assert.Null(proto.Deployment);
#pragma warning restore CS0612
        Assert.Equal(proto, ProtoExtensions.ToProto(value));
        Assert.Equal(value, ProtoExtensions.FromVersioningOverrideProto(proto));
    }

    [Fact]
    public void ToProto_OneTime_SetsTargetWithoutLegacyFields()
    {
        var version = new WorkerDeploymentVersion("deployment", "build.id");
        var value = new VersioningOverride.OneTime(version);
        var proto = value.ToProto();
        Assert.Equal(ApiWorkflow.VersioningOverride.OverrideOneofCase.OneTime, proto.OverrideCase);
        Assert.Equal(version.ToProto(), proto.OneTime.TargetDeploymentVersion);
#pragma warning disable CS0612
        Assert.Equal(Api.Enums.V1.VersioningBehavior.Unspecified, proto.Behavior);
        Assert.Empty(proto.PinnedVersion);
        Assert.Null(proto.Deployment);
#pragma warning restore CS0612
        Assert.Equal(proto, ProtoExtensions.ToProto(value));
        Assert.Equal(value, ProtoExtensions.FromVersioningOverrideProto(proto));
    }

    [Fact]
    public void FromVersioningOverrideProto_NoOverride_ReturnsNull() =>
        Assert.Null(ProtoExtensions.FromVersioningOverrideProto(new()));
}
