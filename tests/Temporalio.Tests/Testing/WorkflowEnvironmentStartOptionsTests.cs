namespace Temporalio.Tests.Testing;

using System;
using Temporalio.Bridge;
using Temporalio.Testing;
using Xunit;

public class WorkflowEnvironmentStartOptionsTests
{
    [Fact]
    public unsafe void ToInteropOptions_LocalStartupTimeout_MapsToMillis()
    {
        using var scope = new Scope();
        var options = new WorkflowEnvironmentStartLocalOptions()
        {
            DevServerOptions = new() { StartupTimeout = TimeSpan.FromMilliseconds(1_500) },
        }.ToInteropOptions(scope);

        Assert.Equal(1_500UL, options.test_server->startup_timeout_millis);
    }

    [Fact]
    public unsafe void ToInteropOptions_LocalStartupTimeoutUnset_LeavesCoreDefault()
    {
        using var scope = new Scope();
        var options = new WorkflowEnvironmentStartLocalOptions().ToInteropOptions(scope);

        Assert.Equal(0UL, options.test_server->startup_timeout_millis);
    }

    [Fact]
    public void ToInteropOptions_TimeSkippingStartupTimeout_MapsToMillis()
    {
        using var scope = new Scope();
        var options = new WorkflowEnvironmentStartTimeSkippingOptions()
        {
            TestServer = new() { StartupTimeout = TimeSpan.FromMilliseconds(1_500) },
        }.ToInteropOptions(scope);

        Assert.Equal(1_500UL, options.startup_timeout_millis);
    }

    [Fact]
    public void ToInteropOptions_TimeSkippingStartupTimeoutUnset_LeavesCoreDefault()
    {
        using var scope = new Scope();
        var options = new WorkflowEnvironmentStartTimeSkippingOptions().ToInteropOptions(scope);

        Assert.Equal(0UL, options.startup_timeout_millis);
    }
}
