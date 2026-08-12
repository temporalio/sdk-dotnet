namespace Temporalio.Tests.Extensions.Gcp.CloudRun.OpenTelemetry;

using global::OpenTelemetry;
using global::OpenTelemetry.Trace;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry;
using Xunit;
using TemporalOpenTelemetry = Temporalio.Extensions.OpenTelemetry;

public class TemporalClientConnectOptionsExtensionsTests
{
    private const string OTelExporterOtlpEndpointEnvironmentVariable =
        "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const string OTelServiceNameEnvironmentVariable = "OTEL_SERVICE_NAME";
    private const string CloudRunWorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
    private const string CloudRunServiceEnvironmentVariable = "K_SERVICE";

    [Fact]
    public void ApplyGoogleCloudRunOpenTelemetryDefaults_NullConfigThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TemporalClientConnectOptionsExtensions.ApplyGoogleCloudRunOpenTelemetryDefaults(null!));
    }

    [Fact]
    public void ResolveOptions_ExplicitOptionsWin()
    {
        using var env = new EnvironmentScope(
            KeyValuePair.Create<string, string?>(
                OTelExporterOtlpEndpointEnvironmentVariable,
                "http://env:4317"),
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                "env-service"),
            KeyValuePair.Create<string, string?>(
                CloudRunWorkerPoolEnvironmentVariable,
                "worker-pool"),
            KeyValuePair.Create<string, string?>(
                CloudRunServiceEnvironmentVariable,
                "k-service"));
        var resolved = TemporalClientConnectOptionsExtensions.ResolveOptions(
            new GoogleCloudRunOpenTelemetryOptions
            {
                CollectorEndpoint = "http://explicit:4317",
                ServiceName = "explicit-service",
                MetricsExportInterval = TimeSpan.FromSeconds(3),
            });

        Assert.Equal(new Uri("http://explicit:4317"), resolved.CollectorEndpoint);
        Assert.Equal("explicit-service", resolved.ServiceName);
        Assert.Equal(TimeSpan.FromSeconds(3), resolved.MetricsExportInterval);
    }

    [Fact]
    public void ResolveOptions_OTelServiceNameWinsOverCloudRunVars()
    {
        using var env = new EnvironmentScope(
            KeyValuePair.Create<string, string?>(
                OTelExporterOtlpEndpointEnvironmentVariable,
                "http://env:4317"),
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                "env-service"),
            KeyValuePair.Create<string, string?>(
                CloudRunWorkerPoolEnvironmentVariable,
                "worker-pool"),
            KeyValuePair.Create<string, string?>(
                CloudRunServiceEnvironmentVariable,
                "k-service"));
        var resolved = TemporalClientConnectOptionsExtensions.ResolveOptions();

        Assert.Equal(new Uri("http://env:4317"), resolved.CollectorEndpoint);
        Assert.Equal("env-service", resolved.ServiceName);
        Assert.Equal(TimeSpan.FromSeconds(60), resolved.MetricsExportInterval);
    }

    [Fact]
    public void ResolveOptions_WorkerPoolWinsOverService()
    {
        using var env = new EnvironmentScope(
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                CloudRunWorkerPoolEnvironmentVariable,
                "worker-pool"),
            KeyValuePair.Create<string, string?>(
                CloudRunServiceEnvironmentVariable,
                "k-service"));
        var resolved = TemporalClientConnectOptionsExtensions.ResolveOptions();

        Assert.Equal("worker-pool", resolved.ServiceName);
    }

    [Fact]
    public void ResolveOptions_ServiceNameFromKService()
    {
        using var env = new EnvironmentScope(
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                CloudRunWorkerPoolEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                CloudRunServiceEnvironmentVariable,
                "k-service"));
        var resolved = TemporalClientConnectOptionsExtensions.ResolveOptions();

        Assert.Equal("k-service", resolved.ServiceName);
    }

    [Fact]
    public void ResolveOptions_UsesFallbacks()
    {
        using var env = new EnvironmentScope(
            KeyValuePair.Create<string, string?>(
                OTelExporterOtlpEndpointEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                CloudRunWorkerPoolEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                CloudRunServiceEnvironmentVariable,
                null));
        var resolved = TemporalClientConnectOptionsExtensions.ResolveOptions();

        Assert.Equal(new Uri("http://localhost:4317"), resolved.CollectorEndpoint);
        Assert.Equal("temporal-worker", resolved.ServiceName);
        Assert.Equal(TimeSpan.FromSeconds(60), resolved.MetricsExportInterval);
    }

    [Fact]
    public void ResolveOptions_InvalidMetricsExportIntervalThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TemporalClientConnectOptionsExtensions.ResolveOptions(
                new GoogleCloudRunOpenTelemetryOptions
                {
                    MetricsExportInterval = TimeSpan.Zero,
                }));
    }

    [Fact]
    public void ApplyGoogleCloudRunOpenTelemetryDefaults_PreservesInterceptorsAndAddsTracing()
    {
        var existingInterceptor = new NoopClientInterceptor();
        var options = new TemporalClientConnectOptions
        {
            Interceptors = new IClientInterceptor[] { existingInterceptor },
        };

        using var handle = options.ApplyGoogleCloudRunOpenTelemetryDefaults();

        var interceptors = Assert.IsAssignableFrom<IReadOnlyCollection<IClientInterceptor>>(
            options.Interceptors);
        Assert.Equal(2, interceptors.Count);
        Assert.Same(existingInterceptor, interceptors.First());
        Assert.IsType<TemporalOpenTelemetry.TracingInterceptor>(interceptors.Last());
    }

    [Fact]
    public async Task ApplyGoogleCloudRunOpenTelemetryDefaults_ConfiguresRuntimeAndReturnsHandle()
    {
        var options = new TemporalClientConnectOptions();

        using var handle = options.ApplyGoogleCloudRunOpenTelemetryDefaults(
            new GoogleCloudRunOpenTelemetryOptions
            {
                CollectorEndpoint = "http://localhost:4317",
                ServiceName = "test-service",
                MetricsExportInterval = TimeSpan.FromSeconds(1),
            });

        Assert.NotNull(options.Runtime);
        Assert.NotNull(handle);
        await handle.FlushAsync(TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ForceFlushAsync_RunsForceFlushOffCallerThread()
    {
        using var flushStarted = new ManualResetEventSlim();
        using var releaseFlush = new ManualResetEventSlim();
#pragma warning disable CA2000 // Tracer provider owns the processor/exporter.
        using var provider = Sdk.CreateTracerProviderBuilder().
            AddProcessor(new SimpleActivityExportProcessor(
                new BlockingForceFlushExporter(flushStarted, releaseFlush))).
            Build();
#pragma warning restore CA2000

#pragma warning disable CA2025 // The task is completed before the provider exits scope.
        var flushTask = TemporalClientConnectOptionsExtensions.ForceFlushAsync(
            provider,
            TimeSpan.FromSeconds(10),
            CancellationToken.None);
#pragma warning restore CA2025

        try
        {
            Assert.True(flushStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(flushTask.IsCompleted);
        }
        finally
        {
            releaseFlush.Set();
            await flushTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task ForceFlushAsync_ReturnsWhenCancellationRequested()
    {
        using var flushStarted = new ManualResetEventSlim();
        using var releaseFlush = new ManualResetEventSlim();
        using var flushCompleted = new ManualResetEventSlim();
#pragma warning disable CA2000 // Tracer provider owns the processor/exporter.
        using var provider = Sdk.CreateTracerProviderBuilder().
            AddProcessor(new SimpleActivityExportProcessor(
                new BlockingForceFlushExporter(flushStarted, releaseFlush, flushCompleted))).
            Build();
#pragma warning restore CA2000
        using var cts = new CancellationTokenSource();

#pragma warning disable CA2025 // The provider exits scope after the blocking flush is released.
        var flushTask = TemporalClientConnectOptionsExtensions.ForceFlushAsync(
            provider,
            TimeSpan.FromSeconds(10),
            cts.Token);
#pragma warning restore CA2025

        try
        {
            Assert.True(flushStarted.Wait(TimeSpan.FromSeconds(5)));
            await cts.CancelAsync();
            await flushTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(flushCompleted.IsSet);
        }
        finally
        {
            releaseFlush.Set();
            Assert.True(flushCompleted.Wait(TimeSpan.FromSeconds(5)));
        }
    }

    private sealed class BlockingForceFlushExporter :
        BaseExporter<System.Diagnostics.Activity>
    {
        private readonly ManualResetEventSlim flushStarted;
        private readonly ManualResetEventSlim releaseFlush;
        private readonly ManualResetEventSlim? flushCompleted;

        public BlockingForceFlushExporter(
            ManualResetEventSlim flushStarted,
            ManualResetEventSlim releaseFlush,
            ManualResetEventSlim? flushCompleted = null)
        {
            this.flushStarted = flushStarted;
            this.releaseFlush = releaseFlush;
            this.flushCompleted = flushCompleted;
        }

        public override ExportResult Export(in Batch<System.Diagnostics.Activity> batch) =>
            ExportResult.Success;

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            flushStarted.Set();
            try
            {
                return releaseFlush.Wait(timeoutMilliseconds);
            }
            finally
            {
                flushCompleted?.Set();
            }
        }
    }

    private sealed class NoopClientInterceptor : IClientInterceptor
    {
        public ClientOutboundInterceptor InterceptClient(
            ClientOutboundInterceptor nextInterceptor) => nextInterceptor;
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string?> previousValues;

        public EnvironmentScope(params KeyValuePair<string, string?>[] values)
        {
            previousValues = values.ToDictionary(
                pair => pair.Key,
                pair => Environment.GetEnvironmentVariable(pair.Key));
            foreach (var pair in values)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        public void Dispose()
        {
            foreach (var pair in previousValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
