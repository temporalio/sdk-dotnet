using System.Collections.Generic;
using Temporalio.Common;

namespace Temporalio.Worker
{
    /// <summary>
    /// Metric meter that does not record during replay.
    /// </summary>
    internal class ReplaySafeMetricMeter : IMetricMeter
    {
        private readonly IMetricMeter underlying;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaySafeMetricMeter" /> class.
        /// </summary>
        /// <param name="underlying">Meter meter to delegate to.</param>
        public ReplaySafeMetricMeter(IMetricMeter underlying) => this.underlying = underlying;

        /// <inheritdoc />
        public IMetric.ICounter CreateCounter(
            string name, string? unit = null, string? description = null) =>
            new Counter(underlying.CreateCounter(name, unit, description));

        /// <inheritdoc />
        public IMetric.IGauge CreateGauge(
            string name, string? unit = null, string? description = null) =>
            new Gauge(underlying.CreateGauge(name, unit, description));

        /// <inheritdoc />
        public IMetric.IHistogram CreateHistogram(
            string name, string? unit = null, string? description = null) =>
            new Histogram(underlying.CreateHistogram(name, unit, description));

        /// <inheritdoc />
        public IMetricMeter WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
            new ReplaySafeMetricMeter(underlying.WithTags(tags));

        private class Counter : IMetric.ICounter
        {
            private readonly IMetric.ICounter underlying;

            internal Counter(IMetric.ICounter underlying) => this.underlying = underlying;

            public string Name => underlying.Name;

            public string? Unit => underlying.Unit;

            public string? Description => underlying.Description;

            public void Add(ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                if (!Workflows.Workflow.Unsafe.IsReplaying)
                {
                    underlying.Add(value, extraTags);
                }
            }

            public IMetric.ICounter WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Counter(underlying.WithTags(tags));
        }

        private class Histogram : IMetric.IHistogram
        {
            private readonly IMetric.IHistogram underlying;

            internal Histogram(IMetric.IHistogram underlying) => this.underlying = underlying;

            public string Name => underlying.Name;

            public string? Unit => underlying.Unit;

            public string? Description => underlying.Description;

            public void Record(ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                if (!Workflows.Workflow.Unsafe.IsReplaying)
                {
                    underlying.Record(value, extraTags);
                }
            }

            public IMetric.IHistogram WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Histogram(underlying.WithTags(tags));
        }

        private class Gauge : IMetric.IGauge
        {
            private readonly IMetric.IGauge underlying;

            internal Gauge(IMetric.IGauge underlying) => this.underlying = underlying;

            public string Name => underlying.Name;

            public string? Unit => underlying.Unit;

            public string? Description => underlying.Description;

            public void Set(ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                if (!Workflows.Workflow.Unsafe.IsReplaying)
                {
                    underlying.Set(value, extraTags);
                }
            }

            public IMetric.IGauge WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Gauge(underlying.WithTags(tags));
        }
    }
}