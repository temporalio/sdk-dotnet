using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Telemetry options for a runtime.
    /// </summary>
    public class TelemetryOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the logging options.
        /// </summary>
        public LoggingOptions? Logging { get; set; } = new();

        /// <summary>
        /// Gets or sets the metrics options.
        /// </summary>
        public MetricsOptions? Metrics { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (TelemetryOptions)MemberwiseClone();
            if (Logging != null)
            {
                copy.Logging = (LoggingOptions)Logging.Clone();
            }
            if (Metrics != null)
            {
                copy.Metrics = (MetricsOptions)Metrics.Clone();
            }
            return copy;
        }
    }
}
