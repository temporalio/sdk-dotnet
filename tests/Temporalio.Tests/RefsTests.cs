namespace Temporalio.Tests;

using Xunit;

public class RefsTests
{
    public interface ISomeBaseInterface
    {
    }

    public interface ISomeInterface : ISomeBaseInterface
    {
    }

    [Fact]
    public void GetUnproxiedType_Simple_Succeeds()
    {
        Assert.Equal(
            typeof(ISomeInterface),
            Refs.GetUnproxiedType(Refs.Create<ISomeInterface>().GetType()));
    }
}