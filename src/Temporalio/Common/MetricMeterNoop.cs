using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <inheritdoc />
    internal class MetricMeterNoop : MetricMeter
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly MetricMeterNoop Instance = new();

        private MetricMeterNoop()
        {
        }

        /// <inheritdoc />
        public override MetricCounter<T> CreateCounter<T>(
            string name, string? unit = null, string? description = null)
            where T : struct => new Counter<T>(new(name, unit, description));

        /// <inheritdoc />
        public override MetricHistogram<T> CreateHistogram<T>(
            string name, string? unit = null, string? description = null)
            where T : struct => new Histogram<T>(new(name, unit, description));

        /// <inheritdoc />
        public override MetricGauge<T> CreateGauge<T>(
            string name, string? unit = null, string? description = null)
            where T : struct => new Gauge<T>(new(name, unit, description));

        /// <inheritdoc />
        public override MetricMeter WithTags(IEnumerable<KeyValuePair<string, object>> tags) => this;

        private class Counter<T> : MetricCounter<T>
            where T : struct
        {
            internal Counter(MetricDetails details)
                : base(details)
            {
            }

            public override void Add(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
            }

            public override MetricCounter<T> WithTags(
                IEnumerable<KeyValuePair<string, object>> tags) => this;
        }

        private class Histogram<T> : MetricHistogram<T>
            where T : struct
        {
            internal Histogram(MetricDetails details)
                : base(details)
            {
            }

            public override void Record(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
            }

            public override MetricHistogram<T> WithTags(
                IEnumerable<KeyValuePair<string, object>> tags) => this;
        }

        private class Gauge<T> : MetricGauge<T>
            where T : struct
        {
            internal Gauge(MetricDetails details)
                : base(details)
            {
            }

            public override void Set(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
            }

            public override MetricGauge<T> WithTags(
                IEnumerable<KeyValuePair<string, object>> tags) => this;
        }
    }
}