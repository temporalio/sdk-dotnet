using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Tracing options for a runtime.
    /// </summary>
    public class TracingOptions : ICloneable
    {
        /// <summary>
        /// Tracing filter options.
        /// </summary>
        public TelemetryFilterOptions Filter { get; set; } = new();

        /// <summary>
        /// Tracing OpenTelemetry collector options.
        /// </summary>
        public OpenTelemetryOptions OpenTelemetry { get; set; } = new();

        /// <summary>
        /// Create unset tracing options.
        /// </summary>
        public TracingOptions() { }

        /// <summary>
        /// Creaet tracing options for the given filter and OpenTelemetry options.
        /// </summary>
        /// <param name="filter"><see cref="TracingOptions.Filter" /></param>
        /// <param name="openTelemetry"><see cref="TracingOptions.OpenTelemetry" /></param>
        public TracingOptions(TelemetryFilterOptions filter, OpenTelemetryOptions openTelemetry)
        {
            Filter = filter;
            OpenTelemetry = openTelemetry;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (TracingOptions)this.MemberwiseClone();
            copy.Filter = (TelemetryFilterOptions)Filter.Clone();
            copy.OpenTelemetry = (OpenTelemetryOptions)OpenTelemetry.Clone();
            return copy;
        }
    }
}
