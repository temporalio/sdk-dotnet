using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <summary>
    /// Base interface for all metrics.
    /// </summary>
    public interface IMetric
    {
        /// <summary>
        /// Metric for adding values as a counter.
        /// </summary>
        public interface ICounter : IMetric
        {
            /// <summary>
            /// Add the given value to the counter.
            /// </summary>
            /// <param name="value">Value to add.</param>
            /// <param name="extraTags">Extra tags if any. If this is called multiple times with the
            /// same tags, use <see cref="WithTags" /> for better performance.</param>
            void Add(ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null);

            /// <summary>
            /// Create a new counter with the given tags.
            /// </summary>
            /// <param name="tags">Tags to append to existing tags.</param>
            /// <returns>New counter.</returns>
            ICounter WithTags(IEnumerable<KeyValuePair<string, object>> tags);
        }

        /// <summary>
        /// Metric for recording values on a histogram.
        /// </summary>
        public interface IHistogram : IMetric
        {
            /// <summary>
            /// Record the given value on the histogram.
            /// </summary>
            /// <param name="value">Value to record.</param>
            /// <param name="extraTags">Extra tags if any. If this is called multiple times with the
            /// same tags, use <see cref="WithTags" /> for better performance.</param>
            void Record(ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null);

            /// <summary>
            /// Create a new histogram with the given tags.
            /// </summary>
            /// <param name="tags">Tags to append to existing tags.</param>
            /// <returns>New histogram.</returns>
            IHistogram WithTags(IEnumerable<KeyValuePair<string, object>> tags);
        }

        /// <summary>
        /// Metric for setting values on a gauge.
        /// </summary>
        public interface IGauge : IMetric
        {
            /// <summary>
            /// Set the given value on the gauge.
            /// </summary>
            /// <param name="value">Value to set.</param>
            /// <param name="extraTags">Extra tags if any. If this is called multiple times with the
            /// same tags, use <see cref="WithTags" /> for better performance.</param>
#pragma warning disable CA1716 // We are ok with using the "Set" name even though "set" is in lang
            void Set(ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null);
#pragma warning restore CA1716

            /// <summary>
            /// Create a new gauge with the given tags.
            /// </summary>
            /// <param name="tags">Tags to append to existing tags.</param>
            /// <returns>New gauge.</returns>
            IGauge WithTags(IEnumerable<KeyValuePair<string, object>> tags);
        }

        /// <summary>
        /// Gets the name for the metric.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the unit for the metric if any.
        /// </summary>
        string? Unit { get; }

        /// <summary>
        /// Gets the description for the metric if any.
        /// </summary>
        string? Description { get; }
    }
}