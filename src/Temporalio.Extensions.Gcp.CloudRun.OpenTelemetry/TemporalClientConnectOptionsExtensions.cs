using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Runtime;
using TemporalOpenTelemetry = Temporalio.Extensions.OpenTelemetry;

namespace Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry extensions for <see cref="TemporalClientConnectOptions" /> for Temporal
    /// workers running on Google Cloud Run, primarily in worker pools.
    /// </summary>
    /// <remarks>WARNING: Google Cloud Run support is experimental.</remarks>
    public static class TemporalClientConnectOptionsExtensions
    {
        private const string DefaultCollectorEndpoint = "http://localhost:4317";
        private const string DefaultServiceName = "temporal-worker";
        private const string OTelExporterOtlpEndpointEnvironmentVariable =
            "OTEL_EXPORTER_OTLP_ENDPOINT";

        private const string OTelServiceNameEnvironmentVariable = "OTEL_SERVICE_NAME";
        private const string CloudRunWorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
        private const string CloudRunServiceEnvironmentVariable = "K_SERVICE";
        private const string ServiceNameResourceAttribute = "service.name";

        /// <summary>
        /// Configure OpenTelemetry metrics and tracing with Google Cloud Run defaults.
        /// </summary>
        /// <param name="options">Client connection options to mutate.</param>
        /// <param name="openTelemetryOptions">Optional OpenTelemetry configuration.</param>
        /// <returns>
        /// A handle that owns the created tracer provider. Dispose it after every worker and client
        /// using these options has stopped to flush and release the provider. Use
        /// <see cref="GoogleCloudRunOpenTelemetryShutdown.FlushAsync" /> to force-flush traces within
        /// a bounded time during graceful shutdown.
        /// </returns>
        /// <remarks>
        /// This creates an OTLP gRPC trace exporter and tracer provider pointed at a local collector
        /// sidecar, configures Core SDK metrics through a Temporal runtime, and adds the Temporal
        /// tracing interceptor. Google Cloud resource detection, authentication, and export are the
        /// responsibility of the collector.
        /// Any existing <see cref="Temporalio.Client.TemporalConnectionOptions.Runtime" /> is
        /// replaced.
        /// </remarks>
        public static GoogleCloudRunOpenTelemetryShutdown ApplyGoogleCloudRunOpenTelemetryDefaults(
            this TemporalClientConnectOptions options,
            GoogleCloudRunOpenTelemetryOptions? openTelemetryOptions = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var resolvedOptions = ResolveOptions(openTelemetryOptions);
#pragma warning disable CA2000 // The returned shutdown handle owns provider disposal.
            var tracerProvider = CreateTracerProvider(resolvedOptions);
#pragma warning restore CA2000

            options.Interceptors = AddTracingInterceptor(options.Interceptors);
            options.Runtime = CreateRuntime(resolvedOptions);
            return new GoogleCloudRunOpenTelemetryShutdown(tracerProvider);
        }

        /// <summary>
        /// Resolve options using process environment variables.
        /// </summary>
        /// <param name="options">Options to resolve.</param>
        /// <returns>Resolved options.</returns>
        internal static ResolvedGoogleCloudRunOpenTelemetryOptions ResolveOptions(
            GoogleCloudRunOpenTelemetryOptions? options = null)
        {
            options ??= new GoogleCloudRunOpenTelemetryOptions();
            if (options.MetricsExportInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MetricsExportInterval must be greater than zero");
            }

            var serviceName = FirstNonEmpty(
                options.ServiceName,
                Environment.GetEnvironmentVariable(OTelServiceNameEnvironmentVariable),
                Environment.GetEnvironmentVariable(CloudRunWorkerPoolEnvironmentVariable),
                Environment.GetEnvironmentVariable(CloudRunServiceEnvironmentVariable),
                DefaultServiceName);
            var collectorEndpoint = FirstNonEmpty(
                options.CollectorEndpoint,
                Environment.GetEnvironmentVariable(OTelExporterOtlpEndpointEnvironmentVariable),
                DefaultCollectorEndpoint);

            return new ResolvedGoogleCloudRunOpenTelemetryOptions(
                new Uri(collectorEndpoint),
                serviceName,
                options.MetricsExportInterval);
        }

        /// <summary>
        /// Force-flush the tracer provider asynchronously.
        /// </summary>
        /// <param name="tracerProvider">Tracer provider to flush.</param>
        /// <param name="flushTimeout">Maximum time to wait for the flush.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task for the flush.</returns>
        internal static async Task ForceFlushAsync(
            TracerProvider tracerProvider,
            TimeSpan flushTimeout,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var flushTask = Task.Run(
                () => tracerProvider.ForceFlush(ToTimeoutMilliseconds(flushTimeout)));
            try
            {
                await flushTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                flushTask.Forget();
            }
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.First(value => !string.IsNullOrEmpty(value))!;

        private static TracerProvider CreateTracerProvider(
            ResolvedGoogleCloudRunOpenTelemetryOptions options) =>
            Sdk.CreateTracerProviderBuilder().
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
#pragma warning disable CS0618 // Cloud Run collector sidecar uses OTLP gRPC on localhost:4317.
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
            ResolvedGoogleCloudRunOpenTelemetryOptions options)
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
