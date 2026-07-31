namespace Temporalio.Tests.Common;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Xunit;

public class ExperimentalAttributePolyfillTests
{
    // Resolving this simple name is itself the guard for the [Embedded] marking: every assembly
    // below exposes its own internal copy to this project via InternalsVisibleTo, so unmarking any
    // one of them fails this file with CS0433 instead of lying dormant until someone writes
    // [Experimental] in a test.
    private static readonly Type Bcl = typeof(ExperimentalAttribute);

    // .NET 10 added Message. The polyfill deliberately stops at the .NET 8 shape, since that is
    // the earliest version shipping the attribute and therefore the only shape valid on every
    // target the SDK could add.
    private static readonly string[] MirroredProperties = { "DiagnosticId", "UrlFormat" };

    public static TheoryData<string> PolyfilledAssemblies() => new()
    {
        "Temporalio",
        "Temporalio.Extensions.Aws.Lambda",
        "Temporalio.Extensions.Aws.Lambda.OpenTelemetry",
        "Temporalio.Extensions.DiagnosticSource",
        "Temporalio.Extensions.Gcp.CloudRun.Id",
        "Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry",
        "Temporalio.Extensions.Hosting",
        "Temporalio.Extensions.OpenTelemetry",
        "Temporalio.Extensions.WorkflowStreams",
    };

    [Theory]
    [MemberData(nameof(PolyfilledAssemblies))]
    public void Polyfill_EveryPackage_IsInternalAndEmbedded(string assemblyName)
    {
        var polyfill = Polyfill(assemblyName);

        // Public would collide with the BCL type for net8+ consumers (CS0433).
        Assert.False(polyfill.IsPublic);
        Assert.True(polyfill.IsSealed);
        Assert.Contains(
            polyfill.GetCustomAttributesData(),
            attr => attr.AttributeType.FullName == "Microsoft.CodeAnalysis.EmbeddedAttribute");
    }

    [Theory]
    [MemberData(nameof(PolyfilledAssemblies))]
    public void Polyfill_EveryPackage_MatchesBclAttributeUsage(string assemblyName)
    {
        var ours = Polyfill(assemblyName).GetCustomAttribute<AttributeUsageAttribute>();
        var theirs = Bcl.GetCustomAttribute<AttributeUsageAttribute>();
        Assert.NotNull(ours);
        Assert.NotNull(theirs);

        // The compiler only honors the attribute where the BCL allows it, so a narrower or wider
        // target list here would silently diverge from the real one.
        Assert.Equal(theirs!.ValidOn, ours!.ValidOn);
        Assert.Equal(theirs.Inherited, ours.Inherited);
        Assert.Equal(theirs.AllowMultiple, ours.AllowMultiple);
    }

    [Fact]
    public void Polyfill_Members_MatchBclShape()
    {
        var ours = Polyfill("Temporalio");

        Assert.Equal(
            Bcl.GetConstructors().Single().GetParameters().Select(p => p.ParameterType),
            ours.GetConstructors().Single().GetParameters().Select(p => p.ParameterType));

        foreach (var name in MirroredProperties)
        {
            var theirs = Bcl.GetProperty(name);
            var mine = ours.GetProperty(name);
            Assert.NotNull(theirs);
            Assert.NotNull(mine);
            Assert.Equal(theirs!.PropertyType, mine!.PropertyType);
            Assert.Equal(theirs.CanRead, mine.CanRead);
            Assert.Equal(theirs.CanWrite, mine.CanWrite);
        }
    }

    private static Type Polyfill(string assemblyName)
    {
        var type = Assembly.Load(assemblyName).GetType(
            "System.Diagnostics.CodeAnalysis.ExperimentalAttribute", throwOnError: false);
        Assert.NotNull(type);
        return type!;
    }
}
