using System;
using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <inheritdoc />
    internal class MetricMeter : IMetricMeter
    {
        private readonly Bridge.MetricAttributes attributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMeter" /> class.
        /// </summary>
        /// <param name="meter">Core meter.</param>
        /// <param name="attributes">Core attributes.</param>
        internal MetricMeter(Bridge.MetricMeter meter, Bridge.MetricAttributes attributes)
        {
            Meter = meter;
            this.attributes = attributes;
        }

        /// <summary>
        /// Gets the core meter.
        /// </summary>
        internal Bridge.MetricMeter Meter { get; private init; }

        /// <inheritdoc />
        public IMetric.ICounter CreateCounter(
            string name, string? unit = null, string? description = null) => new Metric.Counter(
                new(Meter, Bridge.Interop.MetricIntegerKind.Counter, name, unit, description),
                attributes);

        /// <inheritdoc />
        public IMetric.IHistogram CreateHistogram(
            string name, string? unit = null, string? description = null) => new Metric.Histogram(
                new(Meter, Bridge.Interop.MetricIntegerKind.Histogram, name, unit, description),
                attributes);

        /// <inheritdoc />
        public IMetric.IGauge CreateGauge(
            string name, string? unit = null, string? description = null) => new Metric.Gauge(
                new(Meter, Bridge.Interop.MetricIntegerKind.Gauge, name, unit, description),
                attributes);

        /// <inheritdoc />
        public IMetricMeter WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
            new MetricMeter(Meter, attributes.Append(tags));

        /// <summary>
        /// Create a new lazy meter creator.
        /// </summary>
        /// <param name="runtime">Runtime to base on.</param>
        /// <returns>Lazy meter.</returns>
        internal static Lazy<IMetricMeter> LazyFromRuntime(Bridge.Runtime runtime) => new(() =>
            new MetricMeter(runtime.MetricMeter.Value, runtime.MetricMeter.Value.DefaultAttributes));
    }
}