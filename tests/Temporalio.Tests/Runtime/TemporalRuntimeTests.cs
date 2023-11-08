namespace Temporalio.Tests.Runtime;

using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Bridge;
using Temporalio.Client;
using Temporalio.Runtime;
using Temporalio.Worker;
using Temporalio.Workflows;
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

    [Fact]
    public async Task Runtime_CustomMetricMeter_WorksProperly()
    {
        // Create runtime with capturing meter
        var meter = new TestUtils.CaptureMetricMeter();
        var runtime = new TemporalRuntime(new()
        {
            Telemetry = new() { Metrics = new() { CustomMetricMeter = meter } },
        });

        // Create counter metrics w/ different attribute setups
        var counter1 = runtime.MetricMeter.CreateCounter<int>("counter1", "counter1-unit", "counter1-desc");
        counter1.Add(123);
        counter1.Add(234);
        var counter1WithTags = counter1.WithTags(new Dictionary<string, object>()
        {
            { "string-tag", "someval" },
            { "int-tag", 100 },
            { "float-tag", 200.3 },
            { "bool-tag", false },
        });
        counter1WithTags.Add(345);
        counter1WithTags.Add(456, new Dictionary<string, object>()
        {
            { "string-tag", "newval" },
            { "another-int-tag", 300 },
        });
        // Make another counter and record to confirm it's now on a different metric
        runtime.MetricMeter.CreateCounter<int>("counter1", "counter1-unit", "counter1-desc2").Add(567);

        // Check histograms and gauges too
        runtime.MetricMeter.CreateHistogram<int>("hist1", null, "hist1-desc").
            Record(678, new Dictionary<string, object>() { { "string-tag", "histval" } });
        runtime.MetricMeter.CreateGauge<int>("gauge1", "gauge1-unit", null).
            Set(789, new Dictionary<string, object>() { { "string-tag", "gaugeval" } });

        // Check metrics...
        var metrics = meter.Metrics.ToList();
        Assert.Equal(4, metrics.Count);

        Assert.Equal(
            ("counter1", "counter1-unit", "counter1-desc"),
            (metrics[0].Name, metrics[0].Unit, metrics[0].Description));
        var metricValues = metrics[0].Values.ToList();
        Assert.Equal(4, metricValues.Count);
        Assert.Equal(123, metricValues[0].Value);
        Assert.Equal(
            new Dictionary<string, object>() { { "service_name", "temporal-core-sdk" } },
            metricValues[0].Tags);
        Assert.Equal(234, metricValues[1].Value);
        Assert.Equal(
            new Dictionary<string, object>() { { "service_name", "temporal-core-sdk" } },
            metricValues[1].Tags);
        Assert.Equal(345, metricValues[2].Value);
        Assert.Equal(
            new Dictionary<string, object>()
            {
                { "service_name", "temporal-core-sdk" },
                { "string-tag", "someval" },
                { "int-tag", 100L },
                { "float-tag", 200.3D },
                { "bool-tag", false },
            },
            metricValues[2].Tags);
        Assert.Equal(456, metricValues[3].Value);
        Assert.Equal(
            new Dictionary<string, object>()
            {
                { "service_name", "temporal-core-sdk" },
                { "string-tag", "newval" },
                { "int-tag", 100L },
                { "float-tag", 200.3D },
                { "bool-tag", false },
                { "another-int-tag", 300L },
            },
            metricValues[3].Tags);

        Assert.Equal(
            ("counter1", "counter1-unit", "counter1-desc2"),
            (metrics[1].Name, metrics[1].Unit, metrics[1].Description));
        metricValues = metrics[1].Values.ToList();
        Assert.Single(metricValues);
        Assert.Equal(567, metricValues[0].Value);
        Assert.Equal(
            new Dictionary<string, object>() { { "service_name", "temporal-core-sdk" } },
            metricValues[0].Tags);

        Assert.Equal(
            ("hist1", null, "hist1-desc"),
            (metrics[2].Name, metrics[2].Unit, metrics[2].Description));
        metricValues = metrics[2].Values.ToList();
        Assert.Single(metricValues);
        Assert.Equal(678, metricValues[0].Value);
        Assert.Equal(
            new Dictionary<string, object>()
                { { "service_name", "temporal-core-sdk" }, { "string-tag", "histval" } },
            metricValues[0].Tags);

        Assert.Equal(
            ("gauge1", "gauge1-unit", null),
            (metrics[3].Name, metrics[3].Unit, metrics[3].Description));
        metricValues = metrics[3].Values.ToList();
        Assert.Single(metricValues);
        Assert.Equal(789, metricValues[0].Value);
        Assert.Equal(
            new Dictionary<string, object>()
                { { "service_name", "temporal-core-sdk" }, { "string-tag", "gaugeval" } },
            metricValues[0].Tags);
    }

    [Fact]
    public async Task Runtime_LogForwarding_ForwardsProperly()
    {
        // Helper to grab log
        string? lastRunID = null;
        async Task<TestUtils.LogEntry> RunWorkerUntilFailLogAsync(LogForwardingOptions options)
        {
            // Create runtime with custom logger that captures entries
            using var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
            options.Logger = loggerFactory.CreateLogger("my-logger");
            var runtime = new TemporalRuntime(new()
            {
                Telemetry = new() { Logging = new() { Forwarding = options } },
            });

            // Connect client with different runtime
            var client = await TemporalClient.ConnectAsync(new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Options.Namespace,
                Runtime = runtime,
            });

            // Start failing workflow and wait for log
            var workerOpts = new TemporalWorkerOptions($"ts-{Guid.NewGuid()}").
                AddWorkflow<TaskFailWorkflow>();
            using var worker = new TemporalWorker(client, workerOpts);
            return await worker.ExecuteAsync(async () =>
            {
                var handle = await client.StartWorkflowAsync(
                    (TaskFailWorkflow wf) => wf.RunAsync(),
                    new($"wf-{Guid.NewGuid()}", workerOpts.TaskQueue!));
                lastRunID = handle.ResultRunId;
                return await AssertMore.EventuallyAsync(async () =>
                {
                    Assert.NotEmpty(loggerFactory.Logs);
                    return loggerFactory.Logs.First();
                });
            });
        }

        // Try with fields included
        var entry = await RunWorkerUntilFailLogAsync(new());
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.StartsWith("[sdk_core::temporal_sdk_core::worker::workflow] Failing workflow task", entry.Formatted);
        Assert.Contains("\"Intentional error\"", entry.Formatted);
        Assert.IsType<ForwardedLog>(entry.State);
        var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(entry.State);
        var dict = new Dictionary<string, object?>(state);
        Assert.Equal(5, dict.Count);
        Assert.Equal(LogLevel.Warning, dict["Level"]);
        Assert.Equal("temporal_sdk_core::worker::workflow", dict["Target"]);
        Assert.Equal("Failing workflow task", dict["Message"]);
        Assert.True(DateTime.UtcNow.Subtract((DateTime)dict["Timestamp"]!).Duration() < TimeSpan.FromMinutes(5));
        var fields = Assert.IsAssignableFrom<IDictionary<string, JsonElement>>(dict["Fields"]);
        Assert.Contains("Intentional error", fields["failure"].GetString());
        Assert.Equal(lastRunID, fields["run_id"].GetString());

        // Now without fields included
        entry = await RunWorkerUntilFailLogAsync(new() { IncludeFields = false });
        Assert.Equal("[sdk_core::temporal_sdk_core::worker::workflow] Failing workflow task", entry.Formatted);
        state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(entry.State);
        dict = new Dictionary<string, object?>(state);
        Assert.Null(dict["Fields"]);
    }

    [Workflow]
    public class TaskFailWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync() => throw new InvalidOperationException("Intentional error");
    }
}
