using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <summary>
    /// Metric for recording values on a histogram.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public abstract class MetricHistogram<T> : Metric<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetricHistogram{T}" /> class.
        /// </summary>
        /// <param name="details">Details.</param>
        internal MetricHistogram(MetricDetails details)
            : base(details)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricHistogram{T}" /> class.
        /// </summary>
        /// <param name="name">The name of the histogram.</param>
        /// <param name="unit">The optional unit of measurement for the values recorded by the histogram.</param>
        /// <param name="description">The optional description of the histogram.</param>
        protected MetricHistogram(string name, string? unit = null, string? description = null)
            : this(new(name, unit, description))
        {
        }

        /// <summary>
        /// Record the given value on the histogram.
        /// </summary>
        /// <param name="value">Value to record. Currently this can only be a positive
        /// integer.</param>
        /// <param name="extraTags">Extra tags if any. If this is called multiple times with the
        /// same tags, use <see cref="WithTags" /> for better performance.</param>
        public abstract void Record(
            T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null);

        /// <summary>
        /// Create a new histogram with the given tags.
        /// </summary>
        /// <param name="tags">Tags to append to existing tags.</param>
        /// <returns>New histogram.</returns>
        public abstract MetricHistogram<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags);
    }
}