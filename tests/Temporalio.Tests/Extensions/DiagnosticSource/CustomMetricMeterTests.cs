#pragma warning disable SA1201, SA1204 // We want to have classes near their tests
namespace Temporalio.Tests.Extensions.DiagnosticSource;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Extensions.DiagnosticSource;
using Temporalio.Runtime;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class CustomMetricMeterTests : WorkflowEnvironmentTestBase
{
    public CustomMetricMeterTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task CustomMetricMeter_CustomMetricGauge_GroupsProperly()
    {
        // Create meter
        using var meter = new Meter("test-meter");
        var customMeter = new CustomMetricMeter(meter);

        // Create listener
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "test-meter")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        var metrics = new ConcurrentQueue<KeyValuePair<long, Dictionary<string, object>>>();
        meterListener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
            metrics.Enqueue(new(value, new(tags.ToArray().Select(kv =>
                KeyValuePair.Create(kv.Key, kv.Value!))))));
        meterListener.Start();

        // Create a gauge and prepare some tags
        var gauge = customMeter.CreateGauge<long>("test-gauge", null, null);
        var tags1 = customMeter.CreateTags(null, new KeyValuePair<string, object>[]
        {
            new("foo", "bar"),
            new("baz", 1234L),
        });
        var tags2 = customMeter.CreateTags(null, new KeyValuePair<string, object>[]
        {
            new("foo", "baz"),
            new("baz", 1234L),
        });
        // Intentionally matches tags1 but is a separate object and different order
        var tags3 = customMeter.CreateTags(null, new KeyValuePair<string, object>[]
        {
            new("baz", 1234L),
            new("foo", "bar"),
        });
        Assert.Equal(tags1, tags3);
        Assert.Equal(tags1.GetHashCode(), tags3.GetHashCode());

        // Set multiple gauge values, some for the same tag set
        gauge.Set(123, tags1);
        gauge.Set(234, tags1);
        gauge.Set(345, tags2);
        gauge.Set(456, tags3);

        // Confirm since we haven't issued a record request, nothing is in queue
        Assert.Empty(metrics);

        // Issue record request and confirm that we get only 2 measurements due to deduping
        meterListener.RecordObservableInstruments();
        await AssertMore.EqualEventuallyAsync(2, async () => metrics.Count);
        Assert.Single(metrics, kv =>
            kv.Key == 345 && kv.Value["foo"].Equals("baz") && kv.Value["baz"].Equals(1234L));
        Assert.Single(metrics, kv =>
            kv.Key == 456 && kv.Value["foo"].Equals("bar") && kv.Value["baz"].Equals(1234L));

        // Clear out callbacks, change the tags2 one and confirm is as expected
        metrics.Clear();
        gauge.Set(567, tags2);
        meterListener.RecordObservableInstruments();
        await AssertMore.EqualEventuallyAsync(2, async () => metrics.Count);
        Assert.Single(metrics, kv =>
            kv.Key == 567 && kv.Value["foo"].Equals("baz") && kv.Value["baz"].Equals(1234L));
        Assert.Single(metrics, kv =>
            kv.Key == 456 && kv.Value["foo"].Equals("bar") && kv.Value["baz"].Equals(1234L));
    }

    public static class CustomMetricsActivities
    {
        [Activity]
        public static void DoActivity()
        {
            var counter = ActivityExecutionContext.Current.MetricMeter.CreateCounter<short>(
                "my-activity-counter",
                "my-activity-unit",
                "my-activity-description");
            counter.Add(12);
            counter.Add(34, new Dictionary<string, object>() { { "my-activity-extra-tag", 12.34 } });
        }
    }

    [Workflow]
    public class CustomMetricsWorkflow
    {
        [WorkflowRun]
        public async Task RunAsync()
        {
            await Workflow.ExecuteActivityAsync(
                () => CustomMetricsActivities.DoActivity(),
                new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });

            var histogram = Workflow.MetricMeter.CreateHistogram<int>(
                "my-workflow-histogram",
                "my-workflow-unit",
                "my-workflow-description");
            histogram.Record(56);
            histogram.
                WithTags(new Dictionary<string, object>() { { "my-workflow-histogram-tag", 1234 } }).
                Record(78);

            var gauge = Workflow.MetricMeter.CreateGauge<long>("my-workflow-gauge");
            gauge.Set(90);
            gauge.
                WithTags(new Dictionary<string, object>() { { "my-workflow-gauge-tag", "foo" } }).
                Set(91);
        }
    }

    [Fact]
    public async Task CustomMetricMeter_Workflow_RecordsProperly()
    {
        // Create meter
        using var meter = new Meter("test-meter");

        // Create/start listener
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "test-meter")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        var metrics = new ConcurrentQueue<(Instrument Instrument, long Value, Dictionary<string, object> Tags)>();
        meterListener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
            metrics.Enqueue((inst, value, new(tags.ToArray().Select(
                kv => KeyValuePair.Create(kv.Key, kv.Value!))))));
        meterListener.Start();

        // Create runtime/client with meter
        var runtime = new TemporalRuntime(new()
        {
            Telemetry = new()
            {
                Metrics = new()
                {
                    CustomMetricMeter = new CustomMetricMeter(meter),
                    MetricPrefix = "some-prefix_",
                },
            },
        });
        var client = await TemporalClient.ConnectAsync(
            new()
            {
                TargetHost = Client.Connection.Options.TargetHost,
                Namespace = Client.Options.Namespace,
                Runtime = runtime,
            });

        // Run workflow
        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            { Interceptors = new[] { new XunitExceptionInterceptor() } }.
            AddWorkflow<CustomMetricsWorkflow>().
            AddActivity(CustomMetricsActivities.DoActivity));
        await worker.ExecuteAsync(() =>
            client.ExecuteWorkflowAsync(
                (CustomMetricsWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));

        // Record and wait until the gauge at least shows up
        meterListener.RecordObservableInstruments();
        await AssertMore.EventuallyAsync(async () =>
            Assert.Contains(metrics, m => m.Instrument.Name == "my-workflow-gauge"));

        // Check workflow histogram w/ some other sanity checks
        Assert.Single(metrics, m =>
            m.Instrument is Histogram<long> &&
            m.Instrument.Name == "my-workflow-histogram" &&
            m.Instrument.Unit == "my-workflow-unit" &&
            m.Instrument.Description == "my-workflow-description" &&
            m.Value == 56 &&
            m.Tags["namespace"].Equals(client.Options.Namespace) &&
            m.Tags["task_queue"].Equals(worker.Options.TaskQueue) &&
            m.Tags["workflow_type"].Equals("CustomMetricsWorkflow") &&
            !m.Tags.ContainsKey("my-workflow-histogram-tag"));
        Assert.Single(metrics, m =>
            m.Instrument is Histogram<long> &&
            m.Instrument.Name == "my-workflow-histogram" &&
            m.Value == 78 &&
            m.Tags["my-workflow-histogram-tag"].Equals(1234L));

        // Check workflow gauge
        Assert.Single(metrics, m =>
            m.Instrument is ObservableGauge<long> &&
            m.Instrument.Name == "my-workflow-gauge" &&
            m.Instrument.Unit == null &&
            m.Instrument.Description == null &&
            m.Value == 90 &&
            m.Tags["namespace"].Equals(client.Options.Namespace) &&
            m.Tags["task_queue"].Equals(worker.Options.TaskQueue) &&
            m.Tags["workflow_type"].Equals("CustomMetricsWorkflow") &&
            !m.Tags.ContainsKey("my-workflow-gauge-tag"));
        Assert.Single(metrics, m =>
            m.Instrument is ObservableGauge<long> &&
            m.Instrument.Name == "my-workflow-gauge" &&
            m.Value == 91 &&
            m.Tags["my-workflow-gauge-tag"].Equals("foo"));

        // Check activity counter
        Assert.Single(metrics, m =>
            m.Instrument is Counter<long> &&
            m.Instrument.Name == "my-activity-counter" &&
            m.Instrument.Unit == "my-activity-unit" &&
            m.Instrument.Description == "my-activity-description" &&
            m.Value == 12 &&
            m.Tags["namespace"].Equals(client.Options.Namespace) &&
            m.Tags["task_queue"].Equals(worker.Options.TaskQueue) &&
            m.Tags["activity_type"].Equals("DoActivity") &&
            !m.Tags.ContainsKey("my-activity-extra-tag"));
        Assert.Single(metrics, m =>
            m.Instrument is Counter<long> &&
            m.Instrument.Name == "my-activity-counter" &&
            m.Value == 34 &&
            m.Tags["my-activity-extra-tag"].Equals(12.34));

        // Check some known Temporal metrics
        Assert.Single(metrics, m =>
            m.Instrument is Counter<long> &&
            m.Instrument.Name == "some-prefix_workflow_completed" &&
            m.Value == 1 &&
            m.Tags["workflow_type"].Equals("CustomMetricsWorkflow"));
        Assert.Contains(metrics, m =>
            m.Instrument is Histogram<long> &&
            m.Instrument.Name == "some-prefix_workflow_task_execution_latency" &&
            m.Value > 0 &&
            m.Tags["workflow_type"].Equals("CustomMetricsWorkflow"));
        Assert.Single(metrics, m =>
            m.Instrument is ObservableGauge<long> &&
            m.Instrument.Name == "some-prefix_worker_task_slots_available" &&
            m.Value == 100 &&
            m.Tags["worker_type"].Equals("LocalActivityWorker"));
    }
}