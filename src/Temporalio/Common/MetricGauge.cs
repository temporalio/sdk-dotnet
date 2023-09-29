using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <summary>
    /// Metric for setting values on a gauge.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public abstract class MetricGauge<T> : Metric<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetricGauge{T}" /> class.
        /// </summary>
        /// <param name="details">Details.</param>
        internal MetricGauge(MetricDetails details)
            : base(details)
        {
        }

        /// <summary>
        /// Set the given value on the gauge.
        /// </summary>
        /// <param name="value">Value to record. Currently this can only be a positive
        /// integer.</param>
        /// <param name="extraTags">Extra tags if any. If this is called multiple times with the
        /// same tags, use <see cref="WithTags" /> for better performance.</param>
#pragma warning disable CA1716 // We are ok with using the "Set" name even though "set" is in lang
        public abstract void Set(
            T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null);
#pragma warning restore CA1716

        /// <summary>
        /// Create a new gauge with the given tags.
        /// </summary>
        /// <param name="tags">Tags to append to existing tags.</param>
        /// <returns>New gauge.</returns>
        public abstract MetricGauge<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags);
    }
}