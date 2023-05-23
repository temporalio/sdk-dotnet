namespace Temporalio.Tests.Client;

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
}
