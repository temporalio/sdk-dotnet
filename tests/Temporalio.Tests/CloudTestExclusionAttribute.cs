namespace Temporalio.Tests;

using Xunit.Sdk;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
[TraitDiscoverer(
    "Temporalio.Tests.CloudTestExclusionDiscoverer",
    "Temporalio.Tests")]
public sealed class CloudTestExclusionAttribute : Attribute, ITraitAttribute
{
    public CloudTestExclusionAttribute(CloudTestExclusionReason reason, string note)
    {
        Reason = reason;
        Note = note;
    }

    public CloudTestExclusionReason Reason { get; }

    public string Note { get; }
}
