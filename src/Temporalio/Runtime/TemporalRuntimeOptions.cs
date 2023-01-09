using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Options for Temporal runtime.
    /// </summary>
    public class TemporalRuntimeOptions : ICloneable
    {
        /// <summary>
        /// Telemetry options.
        /// </summary>
        public TelemetryOptions Telemetry { get; set; } = new();

        /// <summary>
        /// Create default runtime options.
        /// </summary>
        public TemporalRuntimeOptions() { }

        /// <summary>
        /// Create runtime options with given telemetry options.
        /// </summary>
        /// <param name="telemetry"><see cref="TemporalRuntimeOptions.Telemetry" /></param>
        public TemporalRuntimeOptions(TelemetryOptions telemetry)
        {
            Telemetry = telemetry;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (TemporalRuntimeOptions)this.MemberwiseClone();
            copy.Telemetry = (TelemetryOptions)Telemetry.Clone();
            return copy;
        }
    }
}
