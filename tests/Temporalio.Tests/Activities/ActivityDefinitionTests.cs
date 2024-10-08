namespace Temporalio.Tests.Activities;

using System.Threading.Tasks;
using Temporalio.Activities;
using Temporalio.Converters;
using Xunit;

public class ActivityDefinitionTests
{
    [Fact]
    public void Create_MissingAttribute_Throws()
    {
        var exc = Assert.ThrowsAny<Exception>(() => ActivityDefinition.Create(BadAct1));
        Assert.Contains("missing Activity attribute", exc.Message);
    }

    [Fact]
    public void Create_RefParameter_Throws()
    {
        var exc = Assert.ThrowsAny<Exception>(() => ActivityDefinition.Create(BadAct2));
        Assert.Contains("has disallowed ref/out parameter", exc.Message);
    }

    [Fact]
    public void Create_DefaultNameWithAsync_RemovesAsyncSuffix()
    {
        Assert.Equal("GoodAct1", ActivityDefinition.Create(GoodAct1Async).Name);
    }

    [Fact]
    public void Create_NameOnDynamic_Throws()
    {
        [Activity("CustomName", Dynamic = true)]
        static Task DoThingAsync(IRawValue[] args) => throw new NotImplementedException();
        var exc = Assert.ThrowsAny<Exception>(() => ActivityDefinition.Create(DoThingAsync));
        Assert.Contains("cannot be dynamic and have custom name", exc.Message);
    }

    [Fact]
    public void Create_DynamicInvalidArgs_Throws()
    {
        [Activity(Dynamic = true)]
        static Task DoThingAsync(string arg) => throw new NotImplementedException();
        var exc = Assert.ThrowsAny<Exception>(() => ActivityDefinition.Create(DoThingAsync));
        Assert.Contains("must accept a required array of IRawValue", exc.Message);
    }

    [Fact]
    public void Create_LocalFunctionDefaultNames_AreAccurate()
    {
        [Activity]
        static string StaticDoThing() => string.Empty;
        Assert.Equal("StaticDoThing", ActivityDefinition.Create(StaticDoThing).Name);

        var val = "some val";
        [Activity]
        string DoThing() => val!;
        Assert.Equal("DoThing", ActivityDefinition.Create(DoThing).Name);
    }

    [Fact]
    public void Create_Lambda_Succeeds()
    {
        var def = ActivityDefinition.Create([Activity("MyActivity")] () => string.Empty);
        Assert.Equal("MyActivity", def.Name);
    }

    [Fact]
    public void Create_DefaultNameOnLambda_Throws()
    {
        var exc = Assert.ThrowsAny<Exception>(() =>
            ActivityDefinition.Create([Activity] () => string.Empty));
        Assert.Contains("appears to be a lambda", exc.Message);
    }

    [Fact]
    public void Create_DefaultNameOnGeneric_ProperlyNamed()
    {
        [Activity]
        static Task<T> DoThingAsync<T>(T arg) => throw new NotImplementedException();
        Assert.Equal("DoThing", ActivityDefinition.Create(DoThingAsync<string>).Name);
    }

    [Fact]
    public async Task InvokeAsync_Delegate_CanInvoke()
    {
        var act = [Activity("MyActivity")] (int param) => param + 5;
        Assert.Equal(
            128,
            await ActivityDefinition.Create(act).InvokeAsync(new object?[] { 123 }));
    }

    [Fact]
    public async Task InvokeAsync_DelegateWithDefaultParameter_CanInvoke()
    {
        [Activity]
        int MyActivity(int param = 123) => param + 5;
        Assert.Equal(
            128,
            await ActivityDefinition.Create(MyActivity).InvokeAsync(Array.Empty<object?>()));
        Assert.Equal(
            20,
            await ActivityDefinition.Create(MyActivity).InvokeAsync(new object?[] { 15 }));
    }

    [Fact]
    public async Task InvokeAsync_AsyncDelegate_CanInvoke()
    {
        var act = [Activity("MyActivity")] (int param) => Task.FromResult(param + 5);
        Assert.Equal(
            128,
            await ActivityDefinition.Create(act).InvokeAsync(new object?[] { 123 }));
    }

    [Fact]
    public async Task InvokeAsync_ManualInvoker_IsCalled()
    {
        var defn = ActivityDefinition.Create(
            "some-name",
            typeof(int),
            new Type[] { typeof(int) },
            1,
            parameters => ((int)parameters[0]!) + 5);
        Assert.Equal(128, await defn.InvokeAsync(new object?[] { 123 }));
    }

    [Fact]
    public void CreateAll_ClassWithoutActivities_Throws()
    {
        var exc = Assert.Throws<ArgumentException>(() =>
            ActivityDefinition.CreateAll(
                typeof(BadActivityClassNoActivities), new BadActivityClassNoActivities()));
        Assert.Contains("No activities", exc.Message);
    }

    [Fact]
    public void CreateAll_ClassWithoutInstance_Throws()
    {
        var exc = Assert.Throws<InvalidOperationException>(() =>
            ActivityDefinition.CreateAll(typeof(GoodActivityClassInstance), null));
        Assert.Contains("Instance not provided", exc.Message);
    }

    [Fact]
    public async Task CreateAll_ClassOfActivities_CanInvoke()
    {
        var defn = ActivityDefinition.CreateAll(
            typeof(GoodActivityClassInstance), new GoodActivityClassInstance()).Single();
        Assert.Equal(128, await defn.InvokeAsync(new object?[] { 123 }));
    }

    [Fact]
    public async Task CreateAll_ClassOfStaticActivities_CanInvoke()
    {
        var defn = ActivityDefinition.CreateAll(typeof(GoodActivityClassStatic), null).Single();
        Assert.Equal(128, await defn.InvokeAsync(new object?[] { 123 }));
    }

    [Fact]
    public void CreateAll_OpenGeneric_Throws()
    {
        var exc = Assert.ThrowsAny<Exception>(() => ActivityDefinition.CreateAll(
            typeof(BadActivityGeneric), new BadActivityGeneric()));
        Assert.Contains("contains generic parameters", exc.Message);
    }

    [Fact]
    public async Task CreateAll_ClosedGeneric_CanInvoke()
    {
        var defn = ActivityDefinition.CreateAll(
            typeof(GoodActivityGeneric<string>),
            new GoodActivityGeneric<string>("some-val")).Single();
        Assert.Equal("some-val", await defn.InvokeAsync(Array.Empty<object?>()));
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

    public static class GoodActivityClassStatic
    {
        [Activity]
        public static int MyActivity(int param) => param + 5;
    }

    public class BadActivityGeneric
    {
        [Activity]
        public T BadActAsync<T>() => throw new NotSupportedException();
    }

    public class GoodActivityGeneric<T>
    {
        private readonly T result;

        public GoodActivityGeneric(T result) => this.result = result;

        [Activity]
        public T GoodAsyncAsync() => result;
    }

    public class BadActivityClassNoActivities
    {
        public int ActivityWithoutAttribute(int param) => param + 5;
    }

    public class GoodActivityClassInstance
    {
        [Activity]
        public int MyActivity(int param) => param + 5;
    }
}