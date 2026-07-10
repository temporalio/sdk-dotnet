namespace Temporalio.Tests.Extensions.Aws.Lambda.OpenTelemetry;

using global::OpenTelemetry;
using global::OpenTelemetry.Trace;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Extensions.Aws.Lambda;
using Temporalio.Extensions.Aws.Lambda.OpenTelemetry;
using Xunit;
using TemporalOpenTelemetry = Temporalio.Extensions.OpenTelemetry;

public class TemporalLambdaWorkerOptionsExtensionsTests
{
    private const string OTelExporterOtlpEndpointEnvironmentVariable =
        "OTEL_EXPORTER_OTLP_ENDPOINT";

    private const string OTelServiceNameEnvironmentVariable = "OTEL_SERVICE_NAME";
    private const string LambdaFunctionNameEnvironmentVariable = "AWS_LAMBDA_FUNCTION_NAME";

    [Fact]
    public void ApplyOpenTelemetryDefaults_NullConfigThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TemporalLambdaWorkerOptionsExtensions.ApplyOpenTelemetryDefaults(null!));
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
                LambdaFunctionNameEnvironmentVariable,
                "lambda-service"));
        var resolved = TemporalLambdaWorkerOptionsExtensions.ResolveOptions(new LambdaWorkerOpenTelemetryOptions
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
    public void ResolveOptions_EnvironmentWinsOverFallbacks()
    {
        using var env = new EnvironmentScope(
            KeyValuePair.Create<string, string?>(
                OTelExporterOtlpEndpointEnvironmentVariable,
                "http://env:4317"),
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                "env-service"),
            KeyValuePair.Create<string, string?>(
                LambdaFunctionNameEnvironmentVariable,
                "lambda-service"));
        var resolved = TemporalLambdaWorkerOptionsExtensions.ResolveOptions();

        Assert.Equal(new Uri("http://env:4317"), resolved.CollectorEndpoint);
        Assert.Equal("env-service", resolved.ServiceName);
        Assert.Equal(TimeSpan.FromSeconds(10), resolved.MetricsExportInterval);
    }

    [Fact]
    public void ResolveOptions_LambdaFunctionNameWinsOverDefaultServiceName()
    {
        using var env = new EnvironmentScope(
            KeyValuePair.Create<string, string?>(
                OTelExporterOtlpEndpointEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                LambdaFunctionNameEnvironmentVariable,
                "lambda-service"));
        var resolved = TemporalLambdaWorkerOptionsExtensions.ResolveOptions();

        Assert.Equal(new Uri("http://localhost:4317"), resolved.CollectorEndpoint);
        Assert.Equal("lambda-service", resolved.ServiceName);
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
                LambdaFunctionNameEnvironmentVariable,
                null));
        var resolved = TemporalLambdaWorkerOptionsExtensions.ResolveOptions();

        Assert.Equal(new Uri("http://localhost:4317"), resolved.CollectorEndpoint);
        Assert.Equal("temporal-lambda-worker", resolved.ServiceName);
        Assert.Equal(TimeSpan.FromSeconds(10), resolved.MetricsExportInterval);
    }

    [Fact]
    public void ResolveOptions_InvalidMetricsExportIntervalThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TemporalLambdaWorkerOptionsExtensions.ResolveOptions(
                new LambdaWorkerOpenTelemetryOptions
                {
                    MetricsExportInterval = TimeSpan.Zero,
                }));
    }

    [Fact]
    public void ApplyOpenTelemetryDefaults_PreservesInterceptorsAndAddsTracing()
    {
        var existingInterceptor = new NoopClientInterceptor();
        var config = new TemporalLambdaWorkerOptions
        {
            ClientOptions = new TemporalClientConnectOptions
            {
                Interceptors = new IClientInterceptor[] { existingInterceptor },
            },
        };

        config.ApplyOpenTelemetryDefaults();

        var interceptors = Assert.IsAssignableFrom<IReadOnlyCollection<IClientInterceptor>>(
            config.ClientOptions.Interceptors);
        Assert.Equal(2, interceptors.Count);
        Assert.Same(existingInterceptor, interceptors.First());
        Assert.IsType<TemporalOpenTelemetry.TracingInterceptor>(interceptors.Last());
    }

    [Fact]
    public async Task ApplyOpenTelemetryDefaults_ConfiguresRuntimeAndShutdownHook()
    {
        var config = new TemporalLambdaWorkerOptions
        {
            ShutdownDeadlineBuffer = TimeSpan.FromMilliseconds(1),
        };
        config.AddShutdownHook(_ => Task.CompletedTask);

        config.ApplyOpenTelemetryDefaults(
            new LambdaWorkerOpenTelemetryOptions
            {
                CollectorEndpoint = "http://localhost:4317",
                ServiceName = "test-service",
                MetricsExportInterval = TimeSpan.FromSeconds(1),
            });

        Assert.NotNull(config.ClientOptions.Runtime);
        Assert.Equal(2, config.ShutdownHooks.Count);
        await config.ShutdownHooks[1](CancellationToken.None);
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
        var flushTask = TemporalLambdaWorkerOptionsExtensions.ForceFlushAsync(
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
        var flushTask = TemporalLambdaWorkerOptionsExtensions.ForceFlushAsync(
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
