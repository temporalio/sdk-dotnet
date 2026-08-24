namespace Temporalio.Tests.Runtime;

using System;
using System.IO;
using System.Text;
using Temporalio.Bridge;
using Temporalio.Runtime;
using Xunit;

public class DotnetRuntimeInfoTests
{
    // The runtime type is internal, so it cannot appear in a public xunit test signature.
    [Theory]
    [InlineData("System.Private.CoreLib", true)]
    [InlineData("mscorlib", false)]
    [InlineData(null, false)]
    public void RuntimeTypeOf_CoreLibName_SelectsRuntime(
        string? coreLibSimpleName, bool expectDotnetCore) =>
        Assert.Equal(
            expectDotnetCore ?
                Bridge.Interop.TemporalCoreRuntimeType.DotnetCore :
                Bridge.Interop.TemporalCoreRuntimeType.DotnetFramework,
            DotnetRuntimeInfo.RuntimeTypeOf(coreLibSimpleName));

    [Theory]
    [InlineData("10.0.9-servicing.26270.113+901ca941248413c79832d2fdbd709da0c4386353", "10.0.9")]
    [InlineData("10.0.9-servicing.26270.113", "10.0.9")]
    [InlineData("10.0.9-servicing", "10.0.9")]
    [InlineData("4.8.9037.0 built by: NET48REL1", "4.8.9037.0")]
    [InlineData("4.8.9037.0", "4.8.9037.0")]
    [InlineData("10.0.9+901ca94", "10.0.9")]
    [InlineData("10.0.", "10.0")]
    [InlineData("preview", "")]
    [InlineData("", "")]
    public void NumericVersionPrefix_LabeledVersion_KeepsNumbersOnly(
        string productVersion, string expected) =>
        Assert.Equal(expected, DotnetRuntimeInfo.NumericVersionPrefix(productVersion));

    [Fact]
    public void FileProductVersion_CoreLibOnDisk_ReturnsProductVersion() =>
        Assert.NotEmpty(DotnetRuntimeInfo.FileProductVersion(typeof(object).Assembly.Location));

    [Fact]
    public void FileProductVersion_UnreadableLocation_ReturnsEmpty()
    {
        Assert.Empty(DotnetRuntimeInfo.FileProductVersion(string.Empty));
        Assert.Empty(DotnetRuntimeInfo.FileProductVersion(
            Path.Combine(Path.GetTempPath(), "temporalio-absent-corelib.dll")));
    }

    [Fact]
    public void DetectRuntimeVersion_DotnetCore_ReportsEnvironmentVersion() =>
        Assert.Equal(
            Environment.Version.ToString(),
            DotnetRuntimeInfo.DetectRuntimeVersion(
                Bridge.Interop.TemporalCoreRuntimeType.DotnetCore));

    [Fact]
    public void DetectRuntimeVersion_DotnetFramework_ReturnsNumbersOnly()
    {
        // Under this suite the .NET Framework path reads CoreLib, whose product version is labeled,
        // so a parseable result is what proves normalization reaches a real file.
        var version = DotnetRuntimeInfo.DetectRuntimeVersion(
            Bridge.Interop.TemporalCoreRuntimeType.DotnetFramework);
        Assert.NotEmpty(version);
        Assert.True(Version.TryParse(version, out _), version);
    }

    [Fact]
    public void DetectRuntimeVersion_Unspecified_ReturnsEmpty() =>
        Assert.Empty(DotnetRuntimeInfo.DetectRuntimeVersion(
            Bridge.Interop.TemporalCoreRuntimeType.Unspecified));

    [Fact]
    public void DotnetRuntimeInfo_RunningOnDotnetCore_ReportsTypeAndVersion()
    {
        // Tests consume the netcoreapp3.1 asset, where the type is a compile-time constant;
        // RuntimeTypeOf and DetectRuntimeVersion cover the .NET Framework side.
        Assert.Equal(
            Bridge.Interop.TemporalCoreRuntimeType.DotnetCore, DotnetRuntimeInfo.RuntimeType);
        Assert.Equal(Environment.Version.ToString(), DotnetRuntimeInfo.Version);
    }

    [Fact]
    public unsafe void ToInteropOptions_Default_ReportsSingleRuntime()
    {
        using var scope = new Scope();
        var options = new TemporalRuntimeOptions().ToInteropOptions(scope);

        Assert.Equal((UIntPtr)1, options.runtime_info.size);
        Assert.Equal((byte)0, options.disable_environment_info);

        var runtime = options.runtime_info.data[0];
        Assert.Equal(Bridge.Interop.TemporalCoreRuntimeType.DotnetCore, runtime.runtime_type);
        Assert.Equal(
            DotnetRuntimeInfo.Version,
            Encoding.UTF8.GetString(runtime.version.data, (int)runtime.version.size));
    }

    [Fact]
    public unsafe void ToInteropOptions_DisableEnvironmentInfo_SetsFlag()
    {
        using var scope = new Scope();
        var options = new TemporalRuntimeOptions { DisableEnvironmentInfo = true }.
            ToInteropOptions(scope);

        Assert.Equal((byte)1, options.disable_environment_info);
    }
}
