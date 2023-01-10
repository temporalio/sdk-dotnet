using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Metrics options for a runtime.
    /// <see cref="MetricsOptions.Prometheus" /> or <see cref="MetricsOptions.OpenTelemetry" />
    /// is required (but not both).
    /// </summary>
    public class MetricsOptions : ICloneable
    {
        /// <summary>
        /// Prometheus metrics options.
        /// </summary>
        public PrometheusOptions? Prometheus { get; set; }

        /// <summary>
        /// OpenTelemetry metrics options.
        /// </summary>
        public OpenTelemetryOptions? OpenTelemetry { get; set; }

        /// <summary>
        /// Create unset metrics options.
        /// </summary>
        public MetricsOptions() { }

        /// <summary>
        /// Create Prometheus-based metrics options.
        /// </summary>
        /// <param name="prometheus">Prometheus options.</param>
        public MetricsOptions(PrometheusOptions prometheus)
        {
            Prometheus = prometheus;
        }

        /// <summary>
        /// Create OpenTelemetry-based metrics options.
        /// </summary>
        /// <param name="openTelemetry">OpenTelemetry options.</param>
        public MetricsOptions(OpenTelemetryOptions openTelemetry)
        {
            OpenTelemetry = openTelemetry;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (MetricsOptions)this.MemberwiseClone();
            if (Prometheus != null)
            {
                copy.Prometheus = (PrometheusOptions)Prometheus.Clone();
            }
            if (OpenTelemetry != null)
            {
                copy.OpenTelemetry = (OpenTelemetryOptions)OpenTelemetry.Clone();
            }
            return copy;
        }
    }
}
