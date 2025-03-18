using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <summary>
    /// Metric for adding values as a counter.
    /// </summary>
    /// <typeparam name="T">Type of value for the metric.</typeparam>
    public abstract class MetricCounter<T> : Metric<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetricCounter{T}" /> class.
        /// </summary>
        /// <param name="details">Details.</param>
        internal MetricCounter(MetricDetails details)
            : base(details)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricCounter{T}" /> class.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="unit">The optional unit of measurement for the values recorded by the counter.</param>
        /// <param name="description">The optional description of the counter.</param>
        protected MetricCounter(string name, string? unit = null, string? description = null)
            : this(new(name, unit, description))
        {
        }

        /// <summary>
        /// Add the given value to the counter.
        /// </summary>
        /// <param name="value">Value to add. Currently this can only be a positive integer.</param>
        /// <param name="extraTags">Extra tags if any. If this is called multiple times with the
        /// same tags, use <see cref="WithTags" /> for better performance.</param>
        public abstract void Add(
            T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null);

        /// <summary>
        /// Create a new counter with the given tags.
        /// </summary>
        /// <param name="tags">Tags to append to existing tags.</param>
        /// <returns>New counter.</returns>
        public abstract MetricCounter<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags);
    }
}