namespace Temporalio.Tests.Client;

using System.Collections.Generic;
using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

public class TemporalClientTests : WorkflowEnvironmentTestBase
{
    public TemporalClientTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task ConnectAsync_Connection_Succeeds()
    {
        var resp = await Client.Connection.WorkflowService.GetSystemInfoAsync(
            new Api.WorkflowService.V1.GetSystemInfoRequest());
        // Just confirm the response has a version and capabilities
        Assert.NotEmpty(resp.ServerVersion);
        Assert.NotNull(resp.Capabilities);

        // Update some headers and call again
        // TODO(cretz): Find way to confirm this works without running our own gRPC server
        Client.Connection.RpcMetadata = new Dictionary<string, string> { ["header"] = "value" };
        resp = await Client.Connection.WorkflowService.GetSystemInfoAsync(
            new Api.WorkflowService.V1.GetSystemInfoRequest());
        Assert.NotEmpty(resp.ServerVersion);
        Assert.NotNull(resp.Capabilities);
    }

    [Fact]
    public async Task CreateLazy_Connection_NotConnectedUntilCallMade()
    {
        // Confirm main one is already connected
        Assert.True(Env.Client.Connection.IsConnected);

        // Make a new lazy client and confirm not connected and RPC metadata can't be set
        var client = TemporalClient.CreateLazy(
            (TemporalClientConnectOptions)Env.Client.Connection.Options);
        Assert.False(client.Connection.IsConnected);
        Assert.Throws<InvalidOperationException>(
            () => client.Connection.RpcMetadata = new Dictionary<string, string>());

        // Do health check, confirm connected, confirm RPC metadata can be set
        Assert.True(await client.Connection.CheckHealthAsync());
        Assert.True(client.Connection.IsConnected);
        client.Connection.RpcMetadata = new Dictionary<string, string>();
    }
}
