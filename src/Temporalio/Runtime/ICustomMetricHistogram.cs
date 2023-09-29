namespace Temporalio.Runtime
{
    /// <summary>
    /// Interface to implement for a histogram metric.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public interface ICustomMetricHistogram<T> : ICustomMetric<T>
        where T : struct
    {
        /// <summary>
        /// Record the given value on the histogram.
        /// </summary>
        /// <param name="value">Value to record. Currently this will always be a non-negative
        /// <c>long</c>.</param>
        /// <param name="tags">Tags. This will be the same value/type as returned from
        /// <see cref="ICustomMetricMeter.CreateTags" />.</param>
        void Record(T value, object tags);
    }
}