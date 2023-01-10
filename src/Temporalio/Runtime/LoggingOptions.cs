using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Logging options for a runtime.
    /// </summary>
    public class LoggingOptions : ICloneable
    {
        /// <summary>
        /// Logging filter options.
        /// </summary>
        public TelemetryFilterOptions Filter { get; set; } = new();

        /// <summary>
        /// Create default options.
        /// </summary>
        public LoggingOptions() { }

        /// <summary>
        /// Create options with given filter.
        /// </summary>
        /// <param name="filter">Filter options to set.</param>
        public LoggingOptions(TelemetryFilterOptions filter)
        {
            Filter = filter;
        }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (LoggingOptions)this.MemberwiseClone();
            copy.Filter = (TelemetryFilterOptions)Filter.Clone();
            return copy;
        }
    }
}
