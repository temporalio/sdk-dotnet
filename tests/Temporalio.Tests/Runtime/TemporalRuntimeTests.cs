namespace Temporalio.Tests.Runtime;

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Temporalio.Client;
using Xunit;
using Xunit.Abstractions;

public class TemporalRuntimeTests : WorkflowEnvironmentTestBase
{
    public TemporalRuntimeTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env) { }

    [Fact]
    public async Task Runtime_Separate_BothUsed()
    {
        // Create two clients in separate runtimes with Prometheus endpoints and make calls on them
        var promAddr1 = $"127.0.0.1:{FreePort()}";
        var client1 = await TemporalClient.ConnectAsync(
            new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Namespace,
                Runtime = new(
                    new() { Telemetry = new() { Metrics = new() { Prometheus = new(promAddr1) } } }
                )
            }
        );
        await client1.Connection.WorkflowService.GetSystemInfoAsync(new());
        var promAddr2 = $"127.0.0.1:{FreePort()}";
        var client2 = await TemporalClient.ConnectAsync(
            new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Namespace,
                Runtime = new(
                    new() { Telemetry = new() { Metrics = new() { Prometheus = new(promAddr2) } } }
                )
            }
        );
        await client2.Connection.WorkflowService.GetSystemInfoAsync(new());

        // Check that Prometheus on each runtime is reporting metrics
        using var httpClient = new HttpClient();
        var resp1 = await httpClient.GetAsync($"http://{promAddr1}/metrics");
        Assert.Contains("request{", await resp1.Content.ReadAsStringAsync());
        var resp2 = await httpClient.GetAsync($"http://{promAddr2}/metrics");
        Assert.Contains("request{", await resp1.Content.ReadAsStringAsync());
    }

    private static int FreePort()
    {
        TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
