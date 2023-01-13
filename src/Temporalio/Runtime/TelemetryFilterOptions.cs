using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// A telemetry filter used in logging and tracing.
    /// <see cref="FilterString" /> is required.
    /// </summary>
    public class TelemetryFilterOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryFilterOptions"/> class.
        /// </summary>
        /// <param name="filterString"><see cref="FilterString" />.</param>
        public TelemetryFilterOptions(string filterString)
        {
            FilterString = filterString;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryFilterOptions"/> class.
        /// </summary>
        /// <param name="core">Core level.</param>
        /// <param name="other">Non-core level.</param>
        public TelemetryFilterOptions(Level core = Level.Warn, Level other = Level.Error)
        {
            var coreLevel = core.ToString().ToUpper();
            var otherLevel = other.ToString().ToUpper();
            FilterString =
                $"{otherLevel},temporal_sdk_core={coreLevel},temporal_client={coreLevel},temporal_sdk={coreLevel}";
        }

        /// <summary>
        /// Filter levels.
        /// </summary>
        public enum Level
        {
            /// <summary>
            /// Trace filter level.
            /// </summary>
            Trace,

            /// <summary>
            /// Debug filter level.
            /// </summary>
            Debug,

            /// <summary>
            /// Info filter level.
            /// </summary>
            Info,

            /// <summary>
            /// Warn filter level.
            /// </summary>
            Warn,

            /// <summary>
            /// Error filter level.
            /// </summary>
            Error,
        }

        /// <summary>
        /// Gets or sets the filter string for telemetry filters.
        /// </summary>
        /// <remarks>
        /// This is in the Rust log format. For example, "temporal_sdk_core=DEBUG" sets the level
        /// to <c>DEBUG</c> for the <c>temporal_sdk_core</c> Rust crate.
        /// </remarks>
        public string? FilterString { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
