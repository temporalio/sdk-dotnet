namespace Temporalio.Tests.Nexus;

using Temporalio.Api.Enums.V1;
using Temporalio.Nexus;
using Xunit;

public class NexusOperationStartHelperTests
{
    [Fact]
    public void CreateActivityOnConflictOptions_NotUseExisting_ReturnsNull()
    {
        var result = NexusOperationStartHelper.CreateActivityOnConflictOptions(
            ActivityIdConflictPolicy.Fail, hasLinks: true, hasCompletionCallback: true);
        Assert.Null(result);
    }

    [Fact]
    public void CreateActivityOnConflictOptions_UseExistingWithNeitherArtifact_ReturnsNull()
    {
        var result = NexusOperationStartHelper.CreateActivityOnConflictOptions(
            ActivityIdConflictPolicy.UseExisting, hasLinks: false, hasCompletionCallback: false);
        Assert.Null(result);
    }

    [Fact]
    public void CreateActivityOnConflictOptions_UseExistingWithLinksOnly_AttachesLinksAndRequestIdOnly()
    {
        var result = NexusOperationStartHelper.CreateActivityOnConflictOptions(
            ActivityIdConflictPolicy.UseExisting, hasLinks: true, hasCompletionCallback: false);
        Assert.NotNull(result);
        Assert.True(result.AttachLinks);
        Assert.False(result.AttachCompletionCallbacks);
        Assert.True(result.AttachRequestId);
    }

    [Fact]
    public void CreateActivityOnConflictOptions_UseExistingWithCallbackOnly_AttachesCallbackAndRequestIdOnly()
    {
        var result = NexusOperationStartHelper.CreateActivityOnConflictOptions(
            ActivityIdConflictPolicy.UseExisting, hasLinks: false, hasCompletionCallback: true);
        Assert.NotNull(result);
        Assert.False(result.AttachLinks);
        Assert.True(result.AttachCompletionCallbacks);
        Assert.True(result.AttachRequestId);
    }

    [Fact]
    public void CreateActivityOnConflictOptions_UseExistingWithBoth_AttachesAll()
    {
        var result = NexusOperationStartHelper.CreateActivityOnConflictOptions(
            ActivityIdConflictPolicy.UseExisting, hasLinks: true, hasCompletionCallback: true);
        Assert.NotNull(result);
        Assert.True(result.AttachLinks);
        Assert.True(result.AttachCompletionCallbacks);
        Assert.True(result.AttachRequestId);
    }
}
