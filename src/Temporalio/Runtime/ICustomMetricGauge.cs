namespace Temporalio.Runtime
{
    /// <summary>
    /// Interface to implement for a gauge metric.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public interface ICustomMetricGauge<T> : ICustomMetric<T>
        where T : struct
    {
        /// <summary>
        /// Set the given value on the gauge.
        /// </summary>
        /// <param name="value">Value to set. Currently this will always be a non-negative
        /// <c>long</c>.</param>
        /// <param name="tags">Tags. This will be the same value/type as returned from
        /// <see cref="ICustomMetricMeter.CreateTags" />.</param>
#pragma warning disable CA1716 // We are ok with using the "Set" name even though "set" is in lang
        void Set(T value, object tags);
#pragma warning restore CA1716
    }
}