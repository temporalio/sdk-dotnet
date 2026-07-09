using System;

namespace Temporalio.Extensions.Aws.Lambda.OpenTelemetry
{
    /// <summary>
    /// Resolved OpenTelemetry options for Lambda workers.
    /// </summary>
    internal sealed class ResolvedLambdaWorkerOpenTelemetryOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedLambdaWorkerOpenTelemetryOptions"/> class.
        /// </summary>
        /// <param name="collectorEndpoint">OTLP collector endpoint.</param>
        /// <param name="serviceName">OpenTelemetry service name.</param>
        /// <param name="metricsExportInterval">Metrics export interval.</param>
        public ResolvedLambdaWorkerOpenTelemetryOptions(
            Uri collectorEndpoint,
            string serviceName,
            TimeSpan metricsExportInterval)
        {
            CollectorEndpoint = collectorEndpoint;
            ServiceName = serviceName;
            MetricsExportInterval = metricsExportInterval;
        }

        /// <summary>
        /// Gets the OTLP collector endpoint.
        /// </summary>
        public Uri CollectorEndpoint { get; }

        /// <summary>
        /// Gets the OpenTelemetry service name.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Gets how often the Core SDK exports metrics to the collector.
        /// </summary>
        public TimeSpan MetricsExportInterval { get; }
    }
}
