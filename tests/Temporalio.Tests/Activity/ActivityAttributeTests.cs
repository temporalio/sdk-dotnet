namespace Temporalio.Tests.Activity;

using System.Threading.Tasks;
using Temporalio.Activity;
using Xunit;

public class ActivityAttributeTests
{
    [Fact]
    public void FromDelegate_MissingAttribute_Throws()
    {
        var exc = Assert.ThrowsAny<Exception>(
            () => ActivityAttribute.Definition.FromDelegate(BadAct1));
        Assert.Contains("missing Activity attribute", exc.Message);
    }

    [Fact]
    public void FromDelegate_RefParameter_Throws()
    {
        var exc = Assert.ThrowsAny<Exception>(
            () => ActivityAttribute.Definition.FromDelegate(BadAct2));
        Assert.Contains("has disallowed ref/out parameter", exc.Message);
    }

    [Fact]
    public void FromDelegate_DefaultNameWithAsync_RemovesAsyncSuffix()
    {
        Assert.Equal("GoodAct1", ActivityAttribute.Definition.FromDelegate(GoodAct1Async).Name);
    }

    [Fact]
    public void FromDelegate_LocalFunctionDefaultNames_AreAccurate()
    {
        [Activity]
        static string StaticDoThing() => string.Empty;
        Assert.Equal("StaticDoThing", ActivityAttribute.Definition.FromDelegate(StaticDoThing).Name);

        var val = "some val";
        [Activity]
        string DoThing() => val!;
        Assert.Equal("DoThing", ActivityAttribute.Definition.FromDelegate(DoThing).Name);
    }

    [Fact]
    public void FromDelegate_Lambda_Succeeds()
    {
        var def = ActivityAttribute.Definition.FromDelegate(
            [Activity("MyActivity")] () => string.Empty);
        Assert.Equal("MyActivity", def.Name);
    }

    [Fact]
    public void FromDelegate_DefaultNameOnLambda_Throws()
    {
        var exc = Assert.ThrowsAny<Exception>(() =>
            ActivityAttribute.Definition.FromDelegate(
                [Activity] () => string.Empty));
        Assert.Contains("appears to be a lambda", exc.Message);
    }

    protected static void BadAct1()
    {
    }

    [Activity]
    protected static void BadAct2(ref string foo)
    {
    }

    [Activity]
    protected static Task GoodAct1Async() => Task.CompletedTask;
}