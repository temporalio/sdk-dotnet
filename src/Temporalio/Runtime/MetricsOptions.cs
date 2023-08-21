using System;
using System.Collections.Generic;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Metrics options for a runtime.
    /// <see cref="Prometheus" /> or <see cref="OpenTelemetry" />
    /// is required (but not both).
    /// </summary>
    public class MetricsOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsOptions"/> class.
        /// </summary>
        public MetricsOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsOptions"/> class.
        /// </summary>
        /// <param name="prometheus">Prometheus options.</param>
        public MetricsOptions(PrometheusOptions prometheus) => Prometheus = prometheus;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsOptions"/> class.
        /// </summary>
        /// <param name="openTelemetry">OpenTelemetry options.</param>
        public MetricsOptions(OpenTelemetryOptions openTelemetry) => OpenTelemetry = openTelemetry;

        /// <summary>
        /// Gets or sets the Prometheus metrics options.
        /// </summary>
        public PrometheusOptions? Prometheus { get; set; }

        /// <summary>
        /// Gets or sets the OpenTelemetry metrics options.
        /// </summary>
        public OpenTelemetryOptions? OpenTelemetry { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the service name is set on metrics.
        /// </summary>
        public bool AttachServiceName { get; set; } = true;

        /// <summary>
        /// Gets or sets the tags to be put on every metric. Neither keys nor values may have
        /// newlines.
        /// </summary>
        public IReadOnlyCollection<KeyValuePair<string, string>>? GlobalTags { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (MetricsOptions)MemberwiseClone();
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
