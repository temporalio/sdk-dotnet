using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Temporalio.Client.Interceptors;
using Temporalio.Runtime;
using TemporalOpenTelemetry = Temporalio.Extensions.OpenTelemetry;

namespace Temporalio.Extensions.Aws.Lambda.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry helpers for Temporal workers running inside AWS Lambda.
    /// </summary>
    public static class LambdaWorkerOpenTelemetry
    {
        private const string DefaultCollectorEndpoint = "http://localhost:4317";
        private const string DefaultServiceName = "temporal-lambda-worker";
        private const string OTelExporterOtlpEndpointEnvironmentVariable =
            "OTEL_EXPORTER_OTLP_ENDPOINT";

        private const string OTelServiceNameEnvironmentVariable = "OTEL_SERVICE_NAME";
        private const string LambdaFunctionNameEnvironmentVariable = "AWS_LAMBDA_FUNCTION_NAME";
        private const string ServiceNameResourceAttribute = "service.name";

        /// <summary>
        /// Configure OpenTelemetry metrics and tracing with AWS Lambda defaults.
        /// </summary>
        /// <param name="config">Lambda worker configuration to mutate.</param>
        /// <param name="options">Optional OpenTelemetry configuration.</param>
        /// <remarks>
        /// This creates an OTLP trace exporter and tracer provider, configures Core SDK metrics
        /// through a Temporal runtime, adds the Temporal tracing interceptor, and registers a
        /// per-invocation shutdown hook to force-flush traces before the Lambda invocation ends.
        /// </remarks>
        public static void ApplyDefaults(
            LambdaWorkerConfig config,
            LambdaWorkerOpenTelemetryOptions? options = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var resolvedOptions = ResolveOptions(options);
#pragma warning disable CA2000 // Provider is intentionally retained for Lambda warm invocations.
            var tracerProvider = CreateTracerProvider(resolvedOptions);
#pragma warning restore CA2000

            config.ClientOptions.Interceptors = AddTracingInterceptor(
                config.ClientOptions.Interceptors);
            config.ClientOptions.Runtime = CreateRuntime(resolvedOptions);
            config.ShutdownHooks.Add(
                cancellationToken => ForceFlushAsync(
                    tracerProvider,
                    config.ShutdownDeadlineBuffer,
                    cancellationToken));
        }

        /// <summary>
        /// Resolve options using process environment variables.
        /// </summary>
        /// <param name="options">Options to resolve.</param>
        /// <returns>Resolved options.</returns>
        internal static ResolvedLambdaWorkerOpenTelemetryOptions ResolveOptions(
            LambdaWorkerOpenTelemetryOptions? options = null)
        {
            options ??= new LambdaWorkerOpenTelemetryOptions();
            if (options.MetricsExportInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MetricsExportInterval must be greater than zero");
            }

            var serviceName = FirstNonEmpty(
                options.ServiceName,
                Environment.GetEnvironmentVariable(OTelServiceNameEnvironmentVariable),
                Environment.GetEnvironmentVariable(LambdaFunctionNameEnvironmentVariable),
                DefaultServiceName);
            var collectorEndpoint = FirstNonEmpty(
                options.CollectorEndpoint,
                Environment.GetEnvironmentVariable(OTelExporterOtlpEndpointEnvironmentVariable),
                DefaultCollectorEndpoint);

            return new ResolvedLambdaWorkerOpenTelemetryOptions(
                new Uri(collectorEndpoint),
                serviceName,
                options.MetricsExportInterval);
        }

        /// <summary>
        /// Force-flush the tracer provider asynchronously.
        /// </summary>
        /// <param name="tracerProvider">Tracer provider to flush.</param>
        /// <param name="shutdownDeadlineBuffer">Maximum time to wait for the flush.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task for the flush.</returns>
        internal static async Task ForceFlushAsync(
            TracerProvider tracerProvider,
            TimeSpan shutdownDeadlineBuffer,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Task.Run(
                () => tracerProvider.ForceFlush(ToTimeoutMilliseconds(shutdownDeadlineBuffer))).
                ConfigureAwait(false);
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.First(value => !string.IsNullOrEmpty(value))!;

        private static TracerProvider CreateTracerProvider(
            ResolvedLambdaWorkerOpenTelemetryOptions options) =>
            Sdk.CreateTracerProviderBuilder().
                AddXRayTraceId().
                SetResourceBuilder(
                    ResourceBuilder.CreateDefault().AddService(options.ServiceName)).
                AddSource(
                    TemporalOpenTelemetry.TracingInterceptor.ClientSource.Name,
                    TemporalOpenTelemetry.TracingInterceptor.WorkflowsSource.Name,
                    TemporalOpenTelemetry.TracingInterceptor.ActivitiesSource.Name,
                    TemporalOpenTelemetry.TracingInterceptor.NexusSource.Name).
                AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = options.CollectorEndpoint;
#pragma warning disable CS0618 // ADOT Lambda parity uses OTLP gRPC on localhost:4317.
                    exporterOptions.Protocol = OtlpExportProtocol.Grpc;
#pragma warning restore CS0618
                }).
                Build();

        private static List<IClientInterceptor> AddTracingInterceptor(
            IReadOnlyCollection<IClientInterceptor>? interceptors)
        {
            var newInterceptors = interceptors?.ToList() ?? new List<IClientInterceptor>();
            newInterceptors.Add(new TemporalOpenTelemetry.TracingInterceptor());
            return newInterceptors;
        }

        private static TemporalRuntime CreateRuntime(
            ResolvedLambdaWorkerOpenTelemetryOptions options)
        {
            var openTelemetryOptions = new Temporalio.Runtime.OpenTelemetryOptions(
                options.CollectorEndpoint)
            {
                MetricsExportInterval = options.MetricsExportInterval,
                Protocol = OpenTelemetryProtocol.Grpc,
            };
            return new TemporalRuntime(new TemporalRuntimeOptions
            {
                Telemetry = new TelemetryOptions
                {
                    Metrics = new MetricsOptions(openTelemetryOptions)
                    {
                        GlobalTags = new[]
                        {
                            new KeyValuePair<string, string>(
                                ServiceNameResourceAttribute,
                                options.ServiceName),
                        },
                    },
                },
            });
        }

        private static int ToTimeoutMilliseconds(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                return 0;
            }
            if (timeout.TotalMilliseconds >= int.MaxValue)
            {
                return int.MaxValue;
            }
            return (int)timeout.TotalMilliseconds;
        }
    }
}
