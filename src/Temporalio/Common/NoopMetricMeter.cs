using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <inheritdoc />
    internal class NoopMetricMeter : IMetricMeter
    {
        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static readonly NoopMetricMeter Instance = new();

        private NoopMetricMeter()
        {
        }

        /// <inheritdoc />
        public IMetric.ICounter CreateCounter(
            string name, string? unit = null, string? description = null) =>
            new Counter(name, unit, description);

        /// <inheritdoc />
        public IMetric.IHistogram CreateHistogram(
            string name, string? unit = null, string? description = null) =>
            new Histogram(name, unit, description);

        /// <inheritdoc />
        public IMetric.IGauge CreateGauge(
            string name, string? unit = null, string? description = null) =>
            new Gauge(name, unit, description);

        /// <inheritdoc />
        public IMetricMeter WithTags(IEnumerable<KeyValuePair<string, object>> tags) => this;

        private record Counter(string Name, string? Unit, string? Description) : IMetric.ICounter
        {
            public void Add(
                ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
            }

            public IMetric.ICounter WithTags(IEnumerable<KeyValuePair<string, object>> tags) => this;
        }

        private record Histogram(string Name, string? Unit, string? Description) : IMetric.IHistogram
        {
            public void Record(
                ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
            }

            public IMetric.IHistogram WithTags(IEnumerable<KeyValuePair<string, object>> tags) => this;
        }

        private record Gauge(string Name, string? Unit, string? Description) : IMetric.IGauge
        {
            public void Set(
                ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
            }

            public IMetric.IGauge WithTags(IEnumerable<KeyValuePair<string, object>> tags) => this;
        }
    }
}