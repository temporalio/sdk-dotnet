#pragma warning disable CA1822 // We don't want to force workflow methods to be static

namespace Temporalio.Tests.Workflows;

using Temporalio.Converters;
using Temporalio.Workflows;
using Xunit;

public class WorkflowDefinitionTests
{
    [Fact]
    public void Create_RunAttributeMissing_Throws()
    {
        AssertBad<Bad.IWf1>("does not have a valid WorkflowRun method");
    }

    [Fact]
    public void Create_InterfaceTypeDefaultName_RemovesPrefixedI()
    {
        var def = AssertGood<Good.IWf1>();
        Assert.Equal("Wf1", def.Name);
    }

    [Fact]
    public void Create_AdvancedOverrides_Ok()
    {
        AssertGood<Good.Wf2>();
    }

    [Fact]
    public void Create_NoWorkflowAttribute_Throws()
    {
        AssertBad<Bad.IWf2>("missing Workflow attribute");
    }

    [Fact]
    public void Create_InitAttributeOnMultiple_Throws()
    {
        AssertBad<Bad.Wf1>("WorkflowInit on multiple");
    }

    [Fact]
    public void Create_InitAttributeOnNonPublic_Throws()
    {
        AssertBad<Bad.Wf2>("WorkflowInit on non-public");
    }

    [Fact]
    public void Create_RunMethodOnBaseClassOnly_Throws()
    {
        AssertBad<Bad.Wf1>("must be declared on");
    }

    [Fact]
    public void Create_RunAttributeOnMultiple_Throws()
    {
        AssertBad<Bad.IWf3>("WorkflowRun on multiple");
    }

    [Fact]
    public void Create_RunAttributeOnNonPublic_Throws()
    {
        AssertBad<Bad.Wf2>("WorkflowRun on non-public");
    }

    [Fact]
    public void Create_RunAttributeOnStatic_Throws()
    {
        AssertBad<Bad.Wf4>("WorkflowRun on static");
    }

    [Fact]
    public void Create_RunAttributeNonReturnTask_Throws()
    {
        AssertBad<Bad.IWf4>("must return an instance of Task");
    }

    [Fact]
    public void Create_RunAttributeInitAttributeParamMismatch_Throws()
    {
        AssertBad<Bad.Wf3>("must match parameter types of WorkflowInit");
    }

    [Fact]
    public void Create_SignalAttributeNonReturnTask_Throws()
    {
        AssertBad<Bad.IWf3>("SomeSignal1() must return Task");
    }

    [Fact]
    public void Create_SignalAttributeReturnTaskWithValue_Throws()
    {
        AssertBad<Bad.IWf3>("SomeSignal2Async() must return Task");
    }

    [Fact]
    public void Create_SignalAttributeOnNonPublic_Throws()
    {
        AssertBad<Bad.Wf1>("SomeSignalAsync() must be public");
    }

    [Fact]
    public void Create_SignalAttributeOnStatic_Throws()
    {
        AssertBad<Bad.Wf1>("SomeStaticSignalAsync() cannot be static");
    }

    [Fact]
    public void Create_SignalAttributeOnMultiple_Throws()
    {
        AssertBad<Bad.IWf4>("has more than one signal named SomeSignal1");
    }

    [Fact]
    public void Create_SignalAttributeCustomNameOnMultiple_Throws()
    {
        AssertBad<Bad.IWf4>("has more than one signal named CustomSignal1");
    }

    [Fact]
    public void Create_SignalAttributeNotOnOverride_Throws()
    {
        AssertBad<Bad.Wf1>("SomeVirtualSignalAsync() but not override");
    }

    [Fact]
    public void Create_SignalAttributeDefaultNameWithAsync_RemovesAsync()
    {
        var def = AssertGood<Good.IWf1>();
        Assert.Contains("SomeSignal", def.Signals.Keys);
    }

    [Fact]
    public void Create_QueryAttributeReturnTask_Throws()
    {
        AssertBad<Bad.IWf3>("SomeQuery1Async() cannot return a Task");
    }

    [Fact]
    public void Create_QueryAttributeVoid_Throws()
    {
        AssertBad<Bad.IWf3>("SomeQuery2() must return a value");
    }

    [Fact]
    public void Create_QueryAttributeOnNonPublic_Throws()
    {
        AssertBad<Bad.Wf1>("SomeQuery() must be public");
    }

    [Fact]
    public void Create_QueryAttributeOnStatic_Throws()
    {
        AssertBad<Bad.Wf1>("SomeStaticQuery() cannot be static");
    }

    [Fact]
    public void Create_QueryAttributeOnMultiple_Throws()
    {
        AssertBad<Bad.IWf4>("has more than one query named SomeQuery1");
    }

    [Fact]
    public void Create_QueryAttributeCustomNameOnMultiple_Throws()
    {
        AssertBad<Bad.IWf4>("has more than one query named CustomQuery1");
    }

    [Fact]
    public void Create_QueryAttributeNotOnOverride_Throws()
    {
        AssertBad<Bad.Wf1>("SomeVirtualQuery() but not override");
    }

    [Fact]
    public void Create_QueryAttributeOnStaticProperty_Throws()
    {
        AssertBad<Bad.Wf1>("StaticQueryProp cannot be static");
    }

    [Fact]
    public void Create_QueryAttributeOnProtectedProperty_Throws()
    {
        AssertBad<Bad.Wf1>("ProtectedQueryProp must have public getter");
    }

    [Fact]
    public void Create_Generics_Throws()
    {
        // We disallow generics because it is too complicated to handle at this time
        AssertBad<Bad.Wf5<string>>("has generic type arguments");
        AssertBad<Bad.Wf5<string>>("with WorkflowRun contains generic parameters");
        AssertBad<Bad.Wf5<string>>("with WorkflowSignal contains generic parameters");
        AssertBad<Bad.Wf5<string>>("with WorkflowQuery contains generic parameters");
    }

    [Fact]
    public void Create_BadDynamic_Throws()
    {
        AssertBad<Bad.Wf6>("custom name for dynamic workflow");
        AssertBad<Bad.Wf6>("dynamic workflow must accept an array of IRawValue");
        AssertBad<Bad.Wf6>("more than one dynamic signal");
        AssertBad<Bad.Wf6>("DynamicSignal3Async(System.String, Temporalio.Converters.IRawValue[])" +
            " cannot be dynamic with custom name");
        AssertBad<Bad.Wf6>("DynamicSignal4Async(Temporalio.Converters.IRawValue[])" +
            " must accept string and an array of IRawValue");
        AssertBad<Bad.Wf6>("more than one dynamic query");
        AssertBad<Bad.Wf6>("DynamicQuery3(System.String, Temporalio.Converters.IRawValue[])" +
            " cannot be dynamic with custom name");
        AssertBad<Bad.Wf6>("DynamicQuery4(Temporalio.Converters.IRawValue[])" +
            " must accept string and an array of IRawValue");
    }

    private static void AssertBad<T>(string errContains)
    {
        var err = Assert.ThrowsAny<Exception>(() => WorkflowDefinition.Create(typeof(T)));
        Assert.Contains(errContains, err.Message);
    }

    private static WorkflowDefinition AssertGood<T>() => WorkflowDefinition.Create(typeof(T));

    public static class Bad
    {
        [Workflow]
        public interface IWf1
        {
            void RunWithoutAttribute();
        }

        public interface IWf2
        {
            [WorkflowRun]
            Task RunAsync();
        }

        [Workflow]
        public interface IWf3
        {
            [WorkflowRun]
            Task Run1Async();

            [WorkflowRun]
            Task Run2Async();

            [WorkflowSignal]
            void SomeSignal1();

            [WorkflowSignal]
            Task<string> SomeSignal2Async();

            [WorkflowQuery]
            Task<string> SomeQuery1Async();

            [WorkflowQuery]
            void SomeQuery2();
        }

        [Workflow]
        public interface IWf4
        {
            [WorkflowRun]
            string Run();

            [WorkflowSignal]
            Task SomeSignal1Async();

            [WorkflowSignal]
            Task SomeSignal1Async(string param);

            [WorkflowSignal("CustomSignal1")]
            Task SomeSignal2Async();

            [WorkflowSignal("CustomSignal1")]
            Task SomeSignal3Async();

            [WorkflowQuery]
            string SomeQuery1();

            [WorkflowQuery]
            string SomeQuery1(string param);

            [WorkflowQuery("CustomQuery1")]
            string SomeQuery2();

            [WorkflowQuery("CustomQuery1")]
            string SomeQuery3();
        }

        public abstract class Wf1Base
        {
            [WorkflowRun]
            public Task RunAsync() => Task.CompletedTask;

            [WorkflowSignal]
            public virtual Task SomeVirtualSignalAsync() => Task.CompletedTask;

            [WorkflowQuery]
            public virtual string SomeVirtualQuery() => string.Empty;
        }

        [Workflow]
        public class Wf1 : Wf1Base
        {
            [WorkflowInit]
            public Wf1()
            {
            }

            [WorkflowInit]
            public Wf1(string name)
            {
            }

            [WorkflowQuery]
            public static string? StaticQueryProp { get; }

            [WorkflowQuery]
            protected string? ProtectedQueryProp { get; }

            [WorkflowSignal]
            public static Task SomeStaticSignalAsync() => Task.CompletedTask;

            [WorkflowQuery]
            public static string SomeStaticQuery() => string.Empty;

            public override Task SomeVirtualSignalAsync() => Task.CompletedTask;

            public override string SomeVirtualQuery() => string.Empty;

            [WorkflowSignal]
            protected Task SomeSignalAsync() => Task.CompletedTask;

            [WorkflowQuery]
            protected string SomeQuery() => string.Empty;
        }

        [Workflow]
        public class Wf2
        {
            [WorkflowInit]
            protected Wf2()
            {
            }

            [WorkflowRun]
            protected Task RunAsync() => Task.CompletedTask;
        }

        [Workflow]
        public class Wf3
        {
            [WorkflowInit]
            public Wf3(string param)
            {
            }

            [WorkflowRun]
            public Task RunAsync(int param) => Task.CompletedTask;
        }

        [Workflow]
        public class Wf4
        {
            [WorkflowRun]
            public static Task RunAsync() => Task.CompletedTask;

            [WorkflowSignal]
            public Task SomeSignalAsync() => Task.CompletedTask;
        }

        [Workflow]
        public class Wf5<T>
        {
            [WorkflowRun]
            public Task RunAsync<TLocal>(TLocal _) => Task.CompletedTask;

            [WorkflowSignal]
            public Task SomeSignalAsync<TLocal>(TLocal _) => Task.CompletedTask;

            [WorkflowQuery]
            public string SomeQuery<TLocal>(TLocal _) => string.Empty;
        }

        [Workflow("CustomName", Dynamic = true)]
        public class Wf6
        {
            [WorkflowRun]
            public Task RunAsync(int param) => Task.CompletedTask;

            [WorkflowSignal(Dynamic = true)]
            public Task DynamicSignal1Async(string signalName, IRawValue[] args) => Task.CompletedTask;

            [WorkflowSignal(Dynamic = true)]
            public Task DynamicSignal2Async(string signalName, IRawValue[] args) => Task.CompletedTask;

            [WorkflowSignal("CustomName", Dynamic = true)]
            public Task DynamicSignal3Async(string signalName, IRawValue[] args) => Task.CompletedTask;

            [WorkflowSignal(Dynamic = true)]
            public Task DynamicSignal4Async(IRawValue[] args) => Task.CompletedTask;

            [WorkflowQuery(Dynamic = true)]
            public string DynamicQuery1(string signalName, IRawValue[] args) => string.Empty;

            [WorkflowQuery(Dynamic = true)]
            public string DynamicQuery2(string signalName, IRawValue[] args) => string.Empty;

            [WorkflowQuery("CustomName", Dynamic = true)]
            public string DynamicQuery3(string signalName, IRawValue[] args) => string.Empty;

            [WorkflowQuery(Dynamic = true)]
            public string DynamicQuery4(IRawValue[] args) => string.Empty;
        }
    }

    public static class Good
    {
        [Workflow]
        public interface IWf1
        {
            [WorkflowQuery]
            string SomeQueryProp { get; }

            [WorkflowRun]
            Task RunAsync();

            [WorkflowSignal]
            Task SomeSignalAsync();
        }

        [Workflow]
        public interface IWf2
        {
            [WorkflowRun]
            Task RunAsync(string param1, int param2 = 5);

            [WorkflowSignal]
            Task SomeSignalAsync();

            [WorkflowQuery]
            string SomeQuery();
        }

        public abstract class Wf2Base
        {
            [WorkflowRun]
            public virtual Task RunAsync(string param1, int param2 = 5) => Task.CompletedTask;

            public virtual Task SomeSignalAsync() => Task.CompletedTask;

            [WorkflowQuery]
            public string SomeQuery() => string.Empty;
        }

        [Workflow]
        public class Wf2 : Wf2Base, IWf2
        {
            [WorkflowInit]
            public Wf2(string param1, int param2 = 5)
            {
            }

            [WorkflowQuery]
            public string? SomeQueryProp { get; }

            [WorkflowRun]
            public override Task RunAsync(string param1, int param2 = 5) => Task.CompletedTask;

            [WorkflowSignal]
            public override Task SomeSignalAsync() => Task.CompletedTask;
        }
    }
}