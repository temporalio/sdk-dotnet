using System;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Logging options for a runtime.
    /// </summary>
    public class LoggingOptions : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingOptions"/> class.
        /// </summary>
        public LoggingOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingOptions"/> class.
        /// </summary>
        /// <param name="filter">Filter options to set.</param>
        public LoggingOptions(TelemetryFilterOptions filter)
        {
            Filter = filter;
        }

        /// <summary>
        /// Gets or sets the logging filter options.
        /// </summary>
        public TelemetryFilterOptions Filter { get; set; } = new();

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone()
        {
            var copy = (LoggingOptions)MemberwiseClone();
            copy.Filter = (TelemetryFilterOptions)Filter.Clone();
            return copy;
        }
    }
}
