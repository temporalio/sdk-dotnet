namespace Temporalio.Tests;

using Xunit.Sdk;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
[TraitDiscoverer(
    "Temporalio.Tests.CloudTestExclusionDiscoverer",
    "Temporalio.Tests")]
public sealed class CloudTestExclusionAttribute : Attribute, ITraitAttribute
{
    public CloudTestExclusionAttribute(CloudTestExclusionReason reason) => Reason = reason;

    public CloudTestExclusionReason Reason { get; }
}
