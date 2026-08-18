namespace Temporalio.Tests;

using Xunit.Abstractions;
using Xunit.Sdk;

public sealed class CloudTestExclusionDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var reason = (CloudTestExclusionReason)traitAttribute.GetConstructorArguments().Single();
        var reasonName = reason switch
        {
            CloudTestExclusionReason.CloudUnavailable => nameof(CloudTestExclusionReason.CloudUnavailable),
            CloudTestExclusionReason.NeedsCloudAdaptation =>
                nameof(CloudTestExclusionReason.NeedsCloudAdaptation),
            CloudTestExclusionReason.RequiresLocalServer =>
                nameof(CloudTestExclusionReason.RequiresLocalServer),
            _ => throw new InvalidOperationException($"Unknown exclusion reason: {reason}"),
        };
        yield return new("CloudTest", "Excluded");
        yield return new("CloudTestExclusionReason", reasonName);
    }
}
