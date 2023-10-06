namespace Temporalio.Runtime
{
    /// <summary>
    /// Interface to implement for a counter metric.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public interface ICustomMetricCounter<T> : ICustomMetric<T>
        where T : struct
    {
        /// <summary>
        /// Add the given value to the counter.
        /// </summary>
        /// <param name="value">Value to add. Currently this will always be a non-negative
        /// <c>long</c>.</param>
        /// <param name="tags">Tags. This will be the same value/type as returned from
        /// <see cref="ICustomMetricMeter.CreateTags" />.</param>
        void Add(T value, object tags);
    }
}