using System;
using Temporalio.Client;
using OpenTelemetryConfiguration = Temporalio.Extensions.OpenTelemetry.OpenTelemetryConfiguration;
using OpenTelemetryTracerProviderFactory = Temporalio.Extensions.OpenTelemetry.OpenTelemetryTracerProviderFactory;
using ResolvedOpenTelemetryOptions = Temporalio.Extensions.OpenTelemetry.ResolvedOpenTelemetryOptions;

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
        private const string CloudRunWorkerPoolEnvironmentVariable = "CLOUD_RUN_WORKER_POOL";
        private const string CloudRunServiceEnvironmentVariable = "K_SERVICE";

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
            var tracerProvider = OpenTelemetryTracerProviderFactory.
                CreateTracerProvider(resolvedOptions);
#pragma warning restore CA2000

            OpenTelemetryConfiguration.ApplyToClientOptions(
                options,
                resolvedOptions);
            return new GoogleCloudRunOpenTelemetryShutdown(tracerProvider);
        }

        /// <summary>
        /// Resolve options using process environment variables.
        /// </summary>
        /// <param name="options">Options to resolve.</param>
        /// <returns>Resolved options.</returns>
        internal static ResolvedOpenTelemetryOptions ResolveOptions(
            GoogleCloudRunOpenTelemetryOptions? options = null)
        {
            options ??= new GoogleCloudRunOpenTelemetryOptions();
            return OpenTelemetryConfiguration.ResolveOptions(
                new string?[]
                {
                    options.CollectorEndpoint,
                    Environment.GetEnvironmentVariable(
                        OpenTelemetryConfiguration.OtlpEndpointEnvironmentVariable),
                    DefaultCollectorEndpoint,
                },
                new string?[]
                {
                    options.ServiceName,
                    Environment.GetEnvironmentVariable(
                        OpenTelemetryConfiguration.ServiceNameEnvironmentVariable),
                    Environment.GetEnvironmentVariable(CloudRunWorkerPoolEnvironmentVariable),
                    Environment.GetEnvironmentVariable(CloudRunServiceEnvironmentVariable),
                    DefaultServiceName,
                },
                options.MetricsExportInterval,
                nameof(options));
        }
    }
}
