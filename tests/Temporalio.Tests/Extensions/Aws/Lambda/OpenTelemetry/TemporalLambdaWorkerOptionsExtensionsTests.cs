namespace Temporalio.Tests.Extensions.Aws.Lambda.OpenTelemetry;

using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Extensions.Aws.Lambda;
using Temporalio.Extensions.Aws.Lambda.OpenTelemetry;
using Temporalio.Tests.Extensions.OpenTelemetry;
using Xunit;
using TemporalOpenTelemetry = Temporalio.Extensions.OpenTelemetry;

[Collection(OpenTelemetryEnvironmentDefinition.Name)]
public class TemporalLambdaWorkerOptionsExtensionsTests
{
    private const string OTelExporterOtlpEndpointEnvironmentVariable =
        TemporalOpenTelemetry.OpenTelemetryConfiguration.OtlpEndpointEnvironmentVariable;

    private const string OTelServiceNameEnvironmentVariable =
        TemporalOpenTelemetry.OpenTelemetryConfiguration.ServiceNameEnvironmentVariable;

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
}
