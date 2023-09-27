namespace Temporalio.Tests.Runtime;

using System.Net.Http;
using Temporalio.Client;
using Temporalio.Runtime;
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
        var counter1 = runtime.MetricMeter.CreateCounter("counter1", "counter1-unit", "counter1-desc");
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
        runtime.MetricMeter.CreateCounter("counter1", "counter1-unit", "counter1-desc2").Add(567);

        // Check histograms and gauges too
        runtime.MetricMeter.CreateHistogram("hist1", null, "hist1-desc").
            Record(678, new Dictionary<string, object>() { { "string-tag", "histval" } });
        runtime.MetricMeter.CreateGauge("gauge1", "gauge1-unit", null).
            Set(789, new Dictionary<string, object>() { { "string-tag", "gaugeval" } });

        foreach (var metric in meter.Metrics)
        {
            Console.WriteLine("!!! METRIC: {0} - {1} - {2}", metric.Name, metric.Unit, metric.Description);
            foreach (var (value, tags) in metric.Values)
            {
                Console.WriteLine("    !!! VALUE: {0} - {1}", value, string.Join(", ", tags.Select(kv => $"{kv.Key}: {kv.Value}")));
            }
        }

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
}
