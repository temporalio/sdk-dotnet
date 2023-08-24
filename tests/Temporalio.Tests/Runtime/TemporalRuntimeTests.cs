namespace Temporalio.Tests.Runtime;

using System.Net.Http;
using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

public class TemporalRuntimeTests : WorkflowEnvironmentTestBase
{
    public TemporalRuntimeTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task Runtime_Separate_BothUsed()
    {
        // Create two clients in separate runtimes with Prometheus endpoints and make calls on them
        var promAddr1 = $"127.0.0.1:{TestUtils.FreePort()}";
        var client1 = await TemporalClient.ConnectAsync(
            new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Options.Namespace,
                Runtime = new(
                    new() { Telemetry = new() { Metrics = new() { Prometheus = new(promAddr1) } } }),
            });
        await client1.Connection.WorkflowService.GetSystemInfoAsync(new());
        var promAddr2 = $"127.0.0.1:{TestUtils.FreePort()}";
        var client2 = await TemporalClient.ConnectAsync(
            new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Options.Namespace,
                Runtime = new(
                    new() { Telemetry = new() { Metrics = new() { Prometheus = new(promAddr2) } } }),
            });
        await client2.Connection.WorkflowService.GetSystemInfoAsync(new());

        // Check that Prometheus on each runtime is reporting metrics
        using var httpClient = new HttpClient();
        var resp1 = await httpClient.GetAsync(new Uri($"http://{promAddr1}/metrics"));
        Assert.Contains("temporal_request{", await resp1.Content.ReadAsStringAsync());
        var resp2 = await httpClient.GetAsync(new Uri($"http://{promAddr2}/metrics"));
        Assert.Contains("temporal_request{", await resp1.Content.ReadAsStringAsync());
    }
}
