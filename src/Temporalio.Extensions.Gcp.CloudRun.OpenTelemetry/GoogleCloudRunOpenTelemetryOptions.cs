using System;

namespace Temporalio.Extensions.Gcp.CloudRun.OpenTelemetry
{
    /// <summary>
    /// Options for
    /// <see cref="TemporalClientConnectOptionsExtensions.ApplyGoogleCloudRunOpenTelemetryDefaults"/>.
    /// </summary>
    /// <remarks>WARNING: Google Cloud Run support is experimental.</remarks>
    public class GoogleCloudRunOpenTelemetryOptions
    {
        /// <summary>
        /// Gets or sets how often the Core SDK exports metrics to the collector.
        /// </summary>
        /// <remarks>
        /// Defaults to 60 seconds, matching the coordinated Temporal SDK Google Cloud Run default
        /// and the upstream OpenTelemetry SDK default. This is above Google Cloud's five-second
        /// minimum export interval.
        /// </remarks>
        public TimeSpan MetricsExportInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the OpenTelemetry service name. If unset, this falls back to
        /// OTEL_SERVICE_NAME, then CLOUD_RUN_WORKER_POOL, then K_SERVICE, then "temporal-worker".
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the OTLP collector endpoint. If unset, this falls back to
        /// OTEL_EXPORTER_OTLP_ENDPOINT, then "http://localhost:4317".
        /// </summary>
        public string? CollectorEndpoint { get; set; }
    }
}
