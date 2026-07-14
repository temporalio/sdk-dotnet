namespace Temporalio.Tests.Worker;

using Temporalio.Worker;
using Xunit;

public class NotifyOnSetDictionaryTests
{
    [Fact]
    public void MutationGuard_AppliesToEveryMutation()
    {
        var mutationAllowed = true;
        var dictionary = new NotifyOnSetDictionary<string, string>(
            new Dictionary<string, string> { ["existing"] = "value" },
            (_, _) => { },
            () =>
            {
                if (!mutationAllowed)
                {
                    throw new InvalidOperationException("Mutation not allowed");
                }
            });
        mutationAllowed = false;

        Assert.Throws<InvalidOperationException>(() => dictionary["existing"] = "new-value");
        Assert.Throws<InvalidOperationException>(() => dictionary.Add("new", "value"));
        Assert.Throws<InvalidOperationException>(() =>
            dictionary.Add(new KeyValuePair<string, string>("new", "value")));
        Assert.Throws<InvalidOperationException>(() => dictionary.Remove("existing"));
        Assert.Throws<InvalidOperationException>(() =>
            dictionary.Remove(new KeyValuePair<string, string>("existing", "value")));
        Assert.Throws<InvalidOperationException>(() => dictionary.Clear());
        Assert.Equal("value", dictionary["existing"]);
    }
}
