namespace Temporalio.Runtime
{
    /// <summary>
    /// Options for custom metric meter.
    /// </summary>
    public class CustomMetricMeterOptions
    {
        /// <summary>
        /// Format for duration values in metrics.
        /// </summary>
        public enum DurationFormat
        {
            /// <summary>
            /// Format the value as <c>long</c> milliseconds.
            /// </summary>
            IntegerMilliseconds,

            /// <summary>
            /// Format the value as <c>double</c> seconds.
            /// </summary>
            FloatSeconds,

            /// <summary>
            /// Format the value as <see cref="System.TimeSpan" />.
            /// </summary>
            TimeSpan,
        }

        /// <summary>
        /// Gets or sets how histogram duration values are given to the interface.
        /// </summary>
        public DurationFormat HistogramDurationFormat { get; set; } = DurationFormat.IntegerMilliseconds;

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options and any transitive options fields.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}