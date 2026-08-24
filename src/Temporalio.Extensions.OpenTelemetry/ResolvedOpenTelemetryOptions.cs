using System;

namespace Temporalio.Extensions.OpenTelemetry
{
    /// <summary>
    /// Resolved configuration shared by platform-specific OpenTelemetry extensions.
    /// </summary>
    internal sealed class ResolvedOpenTelemetryOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedOpenTelemetryOptions"/> class.
        /// </summary>
        /// <param name="collectorEndpoint">OTLP collector endpoint.</param>
        /// <param name="serviceName">OpenTelemetry service name.</param>
        /// <param name="metricsExportInterval">Metrics export interval.</param>
        internal ResolvedOpenTelemetryOptions(
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
        internal Uri CollectorEndpoint { get; }

        /// <summary>
        /// Gets the OpenTelemetry service name.
        /// </summary>
        internal string ServiceName { get; }

        /// <summary>
        /// Gets how often the Core SDK exports metrics to the collector.
        /// </summary>
        internal TimeSpan MetricsExportInterval { get; }
    }
}
