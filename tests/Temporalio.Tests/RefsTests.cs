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
    public void GetUnderlyingType_Interface_Succeeds()
    {
        Assert.Equal(
            typeof(ISomeInterface),
            Refs.GetUnderlyingType(Refs.Create<ISomeInterface>().GetType()));
    }

    [Fact]
    public void GetUnderlyingType_Class_Succeeds()
    {
        Assert.Equal(
            typeof(SomeClass),
            Refs.GetUnderlyingType(Refs.Create<SomeClass>().GetType()));
    }

    public class SomeClass : ISomeInterface
    {
        public SomeClass(string _)
        {
        }
    }
}