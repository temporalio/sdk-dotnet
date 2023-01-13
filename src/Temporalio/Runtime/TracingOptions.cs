using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Tracing options for a runtime.
    /// </summary>
    public class TracingOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TracingOptions"/> class.
        /// </summary>
        public TracingOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracingOptions"/> class.
        /// </summary>
        /// <param name="filter"><see cref="Filter" />.</param>
        /// <param name="openTelemetry"><see cref="OpenTelemetry" />.</param>
        public TracingOptions(TelemetryFilterOptions filter, OpenTelemetryOptions openTelemetry)
        {
            Filter = filter;
            OpenTelemetry = openTelemetry;
        }

        /// <summary>
        /// Gets or sets the tracing filter options.
        /// </summary>
        public TelemetryFilterOptions Filter { get; set; } = new();

        /// <summary>
        /// Gets or sets the tracing OpenTelemetry collector options.
        /// </summary>
        public OpenTelemetryOptions OpenTelemetry { get; set; } = new();

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (TracingOptions)MemberwiseClone();
            copy.Filter = (TelemetryFilterOptions)Filter.Clone();
            copy.OpenTelemetry = (OpenTelemetryOptions)OpenTelemetry.Clone();
            return copy;
        }
    }
}
