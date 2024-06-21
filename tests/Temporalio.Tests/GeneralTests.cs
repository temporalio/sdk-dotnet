namespace Temporalio.Tests;

using System.Reflection;
using Xunit;
using Xunit.Abstractions;

public class GeneralTests : TestBase
{
    public GeneralTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void CloneableTypes_InstantiateAndClone_Succeeds()
    {
        // Walk Temporal assemblies finding all classes that are cloneable
        var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t =>
            t.IsClass &&
            t.Namespace is { } ns &&
            ns.StartsWith("Temporalio.") &&
            !ns.StartsWith("Temporalio.Api.") &&
            !ns.StartsWith("Temporalio.Bridge.") &&
            t.GetMethod("Clone", BindingFlags.Public | BindingFlags.Instance) != null));
        // Ensure at least one we know of is there
        Assert.Contains(typeof(Temporalio.Client.Schedules.ScheduleListOptions), types);
        foreach (var type in types)
        {
            // Instantiate and attempt clone
            var noParamConstructor = type.GetConstructors().
                First(c => c.GetParameters().All(p => p.IsOptional));
            var instance = noParamConstructor.Invoke(noParamConstructor.GetParameters()
                .Select(p => p.DefaultValue)
                .ToArray());
            var clone = type.GetMethod("Clone")!.Invoke(instance, null);
            Assert.IsType(type, clone);
        }
    }
}