namespace Temporalio.Tests.Client;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Temporalio.Client;
using Temporalio.Runtime;
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
        var resp = await Client.WorkflowService.GetSystemInfoAsync(
            new Api.WorkflowService.V1.GetSystemInfoRequest());
        // Just confirm the response has a version and capabilities
        Assert.NotEmpty(resp.ServerVersion);
        Assert.NotNull(resp.Capabilities);

        // Update some headers and call again
        // TODO(cretz): Find way to confirm this works without running our own gRPC server
        Client.Connection.RpcMetadata = new Dictionary<string, string> { ["header"] = "value" };
        Client.Connection.ApiKey = "my-api-key";
        resp = await Client.WorkflowService.GetSystemInfoAsync(
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

    [Fact]
    public async Task ConnectAsync_Connection_AllGrpcCallsSupported()
    {
        // The approach we'll take here is to just start the dev server and reflectively make each
        // call in workflow service and operator service and check metrics to confirm that every
        // call got that far into core. This catches cases where we didn't add all string call names
        // in Rust for our C# methods.
        var captureMeter = new CaptureRpcCallsMeter();
        await using var env = await Temporalio.Testing.WorkflowEnvironment.StartLocalAsync(new()
        {
            Runtime = new(new()
            {
                Telemetry = new() { Metrics = new() { CustomMetricMeter = captureMeter } },
            }),
        });

        // Check workflow, operator, and cloud service
        await AssertAllRpcsAsync(captureMeter.Calls, env.Client.WorkflowService);
        await AssertAllRpcsAsync(captureMeter.Calls, env.Client.OperatorService, skip: "AddOrUpdateRemoteCluster");
        await AssertAllRpcsAsync(captureMeter.Calls, env.Client.Connection.CloudService);
    }

    private static async Task AssertAllRpcsAsync<T>(
        ConcurrentQueue<string> actualCalls, T service, params string[] skip)
        where T : RpcService
    {
        // Clear actual calls
        actualCalls.Clear();

        // Make calls and populate expected calls
        var expectedCalls = new List<string>();
        foreach (var method in typeof(T).GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            // Add expected call sans Async suffix
            var call = method.Name.Substring(0, method.Name.Length - 5);
            if (skip.Contains(call))
            {
                continue;
            }
            expectedCalls.Add(call);
            // Make call
            var task = (Task)method.Invoke(
                service,
                new object?[] { Activator.CreateInstance(method.GetParameters()[0].ParameterType), null })!;
#pragma warning disable CA1031 // We're ok swallowing exceptions here
            try
            {
                await task;
            }
            catch
            {
            }
#pragma warning restore CA1031
        }

        // Remove skip from actual calls too and then sort both and compare
        var sortedActualCalls = actualCalls.Where(c => !skip.Contains(c)).ToList();
        sortedActualCalls.Sort();
        expectedCalls.Sort();
        Assert.Equal(expectedCalls, sortedActualCalls);
    }

    private class CaptureRpcCallsMeter : ICustomMetricMeter
    {
        public ConcurrentQueue<string> Calls { get; } = new();

        public ICustomMetricCounter<T> CreateCounter<T>(string name, string? unit, string? description)
            where T : struct => new CaptureRpcCallsMetric<T>(name, Calls);

        public ICustomMetricGauge<T> CreateGauge<T>(string name, string? unit, string? description)
            where T : struct => new CaptureRpcCallsMetric<T>(name, Calls);

        public ICustomMetricHistogram<T> CreateHistogram<T>(string name, string? unit, string? description)
            where T : struct => new CaptureRpcCallsMetric<T>(name, Calls);

        public object CreateTags(
            object? appendFrom, IReadOnlyCollection<KeyValuePair<string, object>> tags)
        {
            var dict = new Dictionary<string, object>();
            if (appendFrom is Dictionary<string, object> appendFromDict)
            {
                foreach (var kv in appendFromDict)
                {
                    dict[kv.Key] = kv.Value;
                }
            }
            foreach (var kv in tags)
            {
                dict[kv.Key] = kv.Value;
            }
            return dict;
        }
    }

    private record CaptureRpcCallsMetric<T>(string Name, ConcurrentQueue<string> Calls) :
        ICustomMetricCounter<T>, ICustomMetricHistogram<T>, ICustomMetricGauge<T>
        where T : struct
    {
        public void Add(T value, object tags)
        {
            if (Name == "temporal_request" || Name == "temporal_long_request")
            {
                var call = (string)((Dictionary<string, object>)tags)["operation"];
                if (!Calls.Contains(call))
                {
                    Calls.Enqueue(call);
                }
            }
        }

        public void Record(T value, object tags)
        {
        }

        public void Set(T value, object tags)
        {
        }
    }
}
