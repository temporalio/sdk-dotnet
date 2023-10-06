using System.Collections.Generic;
using Temporalio.Common;

namespace Temporalio.Worker
{
    /// <summary>
    /// Metric meter that does not record during replay.
    /// </summary>
    internal class ReplaySafeMetricMeter : MetricMeter
    {
        private readonly MetricMeter underlying;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaySafeMetricMeter" /> class.
        /// </summary>
        /// <param name="underlying">Meter meter to delegate to.</param>
        public ReplaySafeMetricMeter(MetricMeter underlying) => this.underlying = underlying;

        /// <inheritdoc />
        public override MetricCounter<T> CreateCounter<T>(
            string name, string? unit = null, string? description = null)
            where T : struct => new Counter<T>(underlying.CreateCounter<T>(name, unit, description));

        /// <inheritdoc />
        public override MetricHistogram<T> CreateHistogram<T>(
            string name, string? unit = null, string? description = null)
            where T : struct => new Histogram<T>(underlying.CreateHistogram<T>(name, unit, description));

        /// <inheritdoc />
        public override MetricGauge<T> CreateGauge<T>(
            string name, string? unit = null, string? description = null)
            where T : struct => new Gauge<T>(underlying.CreateGauge<T>(name, unit, description));

        /// <inheritdoc />
        public override MetricMeter WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
            new ReplaySafeMetricMeter(underlying.WithTags(tags));

        private class Counter<T> : MetricCounter<T>
            where T : struct
        {
            private readonly MetricCounter<T> underlying;

            internal Counter(MetricCounter<T> underlying)
                : base(underlying.Details) => this.underlying = underlying;

            public override void Add(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                if (!Workflows.Workflow.Unsafe.IsReplaying)
                {
                    underlying.Add(value, extraTags);
                }
            }

            public override MetricCounter<T> WithTags(
                IEnumerable<KeyValuePair<string, object>> tags) =>
                new Counter<T>(underlying.WithTags(tags));
        }

        private class Histogram<T> : MetricHistogram<T>
            where T : struct
        {
            private readonly MetricHistogram<T> underlying;

            internal Histogram(MetricHistogram<T> underlying)
                : base(underlying.Details) => this.underlying = underlying;

            public override void Record(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                if (!Workflows.Workflow.Unsafe.IsReplaying)
                {
                    underlying.Record(value, extraTags);
                }
            }

            public override MetricHistogram<T> WithTags(
                IEnumerable<KeyValuePair<string, object>> tags) =>
                new Histogram<T>(underlying.WithTags(tags));
        }

        private class Gauge<T> : MetricGauge<T>
            where T : struct
        {
            private readonly MetricGauge<T> underlying;

            internal Gauge(MetricGauge<T> underlying)
                : base(underlying.Details) => this.underlying = underlying;

            public override void Set(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                if (!Workflows.Workflow.Unsafe.IsReplaying)
                {
                    underlying.Set(value, extraTags);
                }
            }

            public override MetricGauge<T> WithTags(
                IEnumerable<KeyValuePair<string, object>> tags) =>
                new Gauge<T>(underlying.WithTags(tags));
        }
    }
}