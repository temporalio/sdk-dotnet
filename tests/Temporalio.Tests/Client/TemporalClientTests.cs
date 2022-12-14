namespace Temporalio.Tests.Client;

using System;
using Xunit;
using Xunit.Abstractions;

public class TemporalClientTests : WorkflowEnvironmentTestBase
{
    public TemporalClientTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env) { }

    [Fact]
    public async Task ConnectAsync_Connection_Succeeds()
    {
        var resp = await Client.Connection.WorkflowService.GetSystemInfoAsync(
            new Api.WorkflowService.V1.GetSystemInfoRequest()
        );
        // Just confirm the response has a version and capabilities
        Assert.NotEmpty(resp.ServerVersion);
        Assert.NotNull(resp.Capabilities);
    }
}
