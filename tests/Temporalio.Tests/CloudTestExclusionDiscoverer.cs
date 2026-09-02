namespace Temporalio.Tests;

using Xunit.Abstractions;
using Xunit.Sdk;

public sealed class CloudTestExclusionDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var reason = (CloudTestExclusionReason)traitAttribute.GetConstructorArguments().First();
        if (!Enum.IsDefined(typeof(CloudTestExclusionReason), reason))
        {
            throw new InvalidOperationException($"Unknown exclusion reason: {reason}");
        }
        yield return new("CloudTest", "Excluded");
        yield return new("CloudTestExclusionReason", reason.ToString());
    }
}
