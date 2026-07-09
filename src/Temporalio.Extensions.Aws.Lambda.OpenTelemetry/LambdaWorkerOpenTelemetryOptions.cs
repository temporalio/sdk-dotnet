using System;

namespace Temporalio.Extensions.Aws.Lambda.OpenTelemetry
{
    /// <summary>
    /// Options for <see cref="LambdaWorkerOpenTelemetry.ApplyDefaults"/>.
    /// </summary>
    public class LambdaWorkerOpenTelemetryOptions
    {
        /// <summary>
        /// Gets or sets how often the Core SDK exports metrics to the collector.
        /// </summary>
        public TimeSpan MetricsExportInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the OpenTelemetry service name. If unset, this falls back to
        /// OTEL_SERVICE_NAME, then AWS_LAMBDA_FUNCTION_NAME, then "temporal-lambda-worker".
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the OTLP collector endpoint. If unset, this falls back to
        /// OTEL_EXPORTER_OTLP_ENDPOINT, then "http://localhost:4317".
        /// </summary>
        public string? CollectorEndpoint { get; set; }
    }
}
