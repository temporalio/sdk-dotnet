namespace Temporalio.Tests;

using System.Reflection;
using Xunit;
using Xunit.Sdk;

public class CloudTestExclusionTests
{
    [Theory]
    [InlineData(
        nameof(RequiresCloudProvisioning),
        nameof(CloudTestExclusionReason.RequiresCloudProvisioning))]
    [InlineData(
        nameof(NeedsCloudAdaptation),
        nameof(CloudTestExclusionReason.NeedsCloudAdaptation))]
    [InlineData(
        nameof(RequiresLocalServer),
        nameof(CloudTestExclusionReason.RequiresLocalServer))]
    public void GetTraits_Reason_GetsExclusionAndReasonTraits(
        string methodName,
        string expectedReasonName)
    {
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["CloudTest"] = "Excluded",
                ["CloudTestExclusionReason"] = expectedReasonName,
            },
            new CloudTestExclusionDiscoverer().
                GetTraits(GetAttributeInfo(methodName)).
                ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    [Fact]
    public void GetTraits_UnknownReason_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new CloudTestExclusionDiscoverer().
                GetTraits(GetAttributeInfo(nameof(UnknownReason))).
                ToList());
    }

    private static ReflectionAttributeInfo GetAttributeInfo(string methodName)
    {
        var method = typeof(CloudTestExclusionTests).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"Missing test method {methodName}");
        return new(method.CustomAttributes.Single(
            attr => attr.AttributeType == typeof(CloudTestExclusionAttribute)));
    }

    [CloudTestExclusion(CloudTestExclusionReason.RequiresCloudProvisioning)]
    private void RequiresCloudProvisioning()
    {
    }

    [CloudTestExclusion(CloudTestExclusionReason.NeedsCloudAdaptation)]
    private void NeedsCloudAdaptation()
    {
    }

    [CloudTestExclusion(CloudTestExclusionReason.RequiresLocalServer)]
    private void RequiresLocalServer()
    {
    }

    [CloudTestExclusion((CloudTestExclusionReason)int.MaxValue)]
    private void UnknownReason()
    {
    }
}
