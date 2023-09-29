using System.Collections.Generic;

namespace Temporalio.Runtime
{
    /// <summary>
    /// Interface to implement to support custom metric handling.
    /// </summary>
    public interface ICustomMetricMeter
    {
        /// <summary>
        /// Create a metric counter.
        /// </summary>
        /// <typeparam name="T">The type of counter value. Currently this is always
        /// <c>long</c>.</typeparam>
        /// <param name="name">Name for the metric.</param>
        /// <param name="unit">Unit for the metric if any.</param>
        /// <param name="description">Description for the metric if any.</param>
        /// <returns>Counter to be called with updates.</returns>
        ICustomMetricCounter<T> CreateCounter<T>(
            string name, string? unit, string? description)
            where T : struct;

        /// <summary>
        /// Create a metric histogram.
        /// </summary>
        /// <typeparam name="T">The type of histogram value. Currently this is always
        /// <c>long</c>.</typeparam>
        /// <param name="name">Name for the metric.</param>
        /// <param name="unit">Unit for the metric if any.</param>
        /// <param name="description">Description for the metric if any.</param>
        /// <returns>Histogram to be called with updates.</returns>
        ICustomMetricHistogram<T> CreateHistogram<T>(
            string name, string? unit, string? description)
            where T : struct;

        /// <summary>
        /// Create a metric gauge.
        /// </summary>
        /// <typeparam name="T">The type of gauge value. Currently this is always
        /// <c>long</c>.</typeparam>
        /// <param name="name">Name for the metric.</param>
        /// <param name="unit">Unit for the metric if any.</param>
        /// <param name="description">Description for the metric if any.</param>
        /// <returns>Gauge to be called with updates.</returns>
        ICustomMetricGauge<T> CreateGauge<T>(
            string name, string? unit, string? description)
            where T : struct;

        /// <summary>
        /// Create a new tag set. This created value will be passed to different metric update
        /// calls at update time.
        /// </summary>
        /// <param name="appendFrom">If present, the new tag set should start with these values. Do
        /// not mutate this value.</param>
        /// <param name="tags">Set of tags. The values of each pair are either <c>string</c>,
        /// <c>long</c>, <c>double</c>, or <c>bool</c>.</param>
        /// <returns>New tag set to use for metric updates.</returns>
        object CreateTags(
            object? appendFrom, IReadOnlyCollection<KeyValuePair<string, object>> tags);
    }
}