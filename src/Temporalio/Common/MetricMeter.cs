using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <summary>
    /// Meter for creating metrics to record values on.
    /// </summary>
    public abstract class MetricMeter
    {
        /// <summary>
        /// Create a new counter. Performance is better if this counter is reused instead of
        /// recreating it.
        /// </summary>
        /// <typeparam name="T">Type of value for the metric. Currently this must be an integer
        /// type.</typeparam>
        /// <param name="name">Name for the counter.</param>
        /// <param name="unit">Unit for the counter if any.</param>
        /// <param name="description">Description for the counter if any.</param>
        /// <returns>New counter.</returns>
        public abstract MetricCounter<T> CreateCounter<T>(
            string name, string? unit = null, string? description = null)
            where T : struct;

        /// <summary>
        /// Create a new histogram. Performance is better if this histogram is reused instead of
        /// recreating it.
        /// </summary>
        /// <typeparam name="T">Type of value for the metric. Currently this must be an integer,
        /// float, or <see cref="System.TimeSpan"/> type.
        /// type.</typeparam>
        /// <param name="name">Name for the histogram.</param>
        /// <param name="unit">Unit for the histogram if any.</param>
        /// <param name="description">Description for the histogram if any.</param>
        /// <returns>New histogram.</returns>
        public abstract MetricHistogram<T> CreateHistogram<T>(
            string name, string? unit = null, string? description = null)
            where T : struct;

        /// <summary>
        /// Create a new gauge. Performance is better if this gauge is reused instead of recreating
        /// it.
        /// </summary>
        /// <typeparam name="T">Type of value for the metric. Currently this must be an integer or
        /// float type.</typeparam>
        /// <param name="name">Name for the gauge.</param>
        /// <param name="unit">Unit for the gauge if any.</param>
        /// <param name="description">Description for the gauge if any.</param>
        /// <returns>New gauge.</returns>
        public abstract MetricGauge<T> CreateGauge<T>(
            string name, string? unit = null, string? description = null)
            where T : struct;

        /// <summary>
        /// Create a new meter with the given tags appended. All metrics created off the meter will
        /// have the tags.
        /// </summary>
        /// <param name="tags">Tags to append.</param>
        /// <returns>New meter.</returns>
        public abstract MetricMeter WithTags(IEnumerable<KeyValuePair<string, object>> tags);
    }
}