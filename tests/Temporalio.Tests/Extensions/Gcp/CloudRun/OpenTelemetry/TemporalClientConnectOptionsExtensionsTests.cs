namespace Temporalio.Tests.Extensions.Gcp.CloudRun.OpenTelemetry;

using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry;
using Temporalio.Tests.Extensions.OpenTelemetry;
using Xunit;
using TemporalOpenTelemetry = Temporalio.Extensions.OpenTelemetry;

[Collection(OpenTelemetryEnvironmentDefinition.Name)]
public class TemporalClientConnectOptionsExtensionsTests
{
    private const string OTelExporterOtlpEndpointEnvironmentVariable =
        TemporalOpenTelemetry.OpenTelemetryConfiguration.OtlpEndpointEnvironmentVariable;

    private const string OTelServiceNameEnvironmentVariable =
        TemporalOpenTelemetry.OpenTelemetryConfiguration.ServiceNameEnvironmentVariable;

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
                " "),
            KeyValuePair.Create<string, string?>(
                OTelServiceNameEnvironmentVariable,
                "\t"),
            KeyValuePair.Create<string, string?>(
                CloudRunWorkerPoolEnvironmentVariable,
                null),
            KeyValuePair.Create<string, string?>(
                CloudRunServiceEnvironmentVariable,
                null));
        var resolved = TemporalClientConnectOptionsExtensions.ResolveOptions(
            new GoogleCloudRunOpenTelemetryOptions
            {
                CollectorEndpoint = "\r\n",
                ServiceName = " ",
            });

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
}
