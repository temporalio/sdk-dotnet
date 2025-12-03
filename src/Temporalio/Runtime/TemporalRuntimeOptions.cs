using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Options for Temporal runtime.
    /// </summary>
    public class TemporalRuntimeOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalRuntimeOptions"/> class.
        /// </summary>
        public TemporalRuntimeOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalRuntimeOptions"/> class.
        /// </summary>
        /// <param name="telemetry"><see cref="Telemetry" />.</param>
        /// <param name="workerHeartbeatInterval">Worker heartbeat interval. If 0 is specified, heartbeat is disabled.</param>
        public TemporalRuntimeOptions(TelemetryOptions telemetry, TimeSpan workerHeartbeatInterval)
        {
            Telemetry = telemetry;
            WorkerHeartbeatInterval = workerHeartbeatInterval;
        }

        /// <summary>
        /// Gets or sets the telemetry options.
        /// </summary>
        public TelemetryOptions Telemetry { get; set; } = new();

        /// <summary>
        /// Gets or sets the worker heartbeat duration in milliseconds.
        /// </summary>
        public TimeSpan WorkerHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (TemporalRuntimeOptions)MemberwiseClone();
            copy.Telemetry = (TelemetryOptions)Telemetry.Clone();
            return copy;
        }
    }
}
