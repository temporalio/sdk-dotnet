using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <inheritdoc />
    internal abstract class Metric : IMetric
    {
        private readonly Bridge.MetricAttributes attributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Metric" /> class.
        /// </summary>
        /// <param name="details">Details.</param>
        /// <param name="attributes">Attributes.</param>
        internal Metric(MetricDetails details, Bridge.MetricAttributes attributes)
        {
            Details = details;
            this.attributes = attributes;
        }

        /// <inheritdoc />
        public string Name => Details.Name;

        /// <inheritdoc />
        public string? Unit => Details.Unit;

        /// <inheritdoc />
        public string? Description => Details.Description;

        /// <summary>
        /// Gets details of the metric.
        /// </summary>
        internal MetricDetails Details { get; private init; }

        /// <summary>
        /// Record metric value.
        /// </summary>
        /// <param name="value">Value to record.</param>
        /// <param name="extraTags">Extra tags.</param>
        internal void Record(
            ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags)
        {
            if (extraTags is { } extraAttrs)
            {
                using (var withExtras = attributes.Append(extraAttrs))
                {
                    Details.Metric.Record(value, withExtras);
                }
            }
            else
            {
                Details.Metric.Record(value, attributes);
            }
        }

        /// <inheritdoc />
        internal class Counter : Metric, IMetric.ICounter
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Counter" /> class.
            /// </summary>
            /// <param name="details">Details.</param>
            /// <param name="attributes">Attributes.</param>
            internal Counter(MetricDetails details, Bridge.MetricAttributes attributes)
                : base(details, attributes)
            {
            }

            /// <inheritdoc />
            public void Add(
                ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null) =>
                Record(value, extraTags);

            /// <inheritdoc />
            public IMetric.ICounter WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Counter(Details, attributes.Append(tags));
        }

        /// <inheritdoc />
        internal class Histogram : Metric, IMetric.IHistogram
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Histogram" /> class.
            /// </summary>
            /// <param name="details">Details.</param>
            /// <param name="attributes">Attributes.</param>
            internal Histogram(MetricDetails details, Bridge.MetricAttributes attributes)
                : base(details, attributes)
            {
            }

            /// <inheritdoc />
            public new void Record(
                ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null) =>
                base.Record(value, extraTags);

            /// <inheritdoc />
            public IMetric.IHistogram WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Histogram(Details, attributes.Append(tags));
        }

        /// <inheritdoc />
        internal class Gauge : Metric, IMetric.IGauge
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Gauge" /> class.
            /// </summary>
            /// <param name="details">Details.</param>
            /// <param name="attributes">Attributes.</param>
            internal Gauge(MetricDetails details, Bridge.MetricAttributes attributes)
                : base(details, attributes)
            {
            }

            /// <inheritdoc />
            public void Set(
                ulong value, IEnumerable<KeyValuePair<string, object>>? extraTags = null) =>
                Record(value, extraTags);

            /// <inheritdoc />
            public IMetric.IGauge WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Gauge(Details, attributes.Append(tags));
        }

        /// <summary>
        /// Metric details for the metric.
        /// </summary>
        /// <param name="Name">Name.</param>
        /// <param name="Unit">Unit.</param>
        /// <param name="Description">Description.</param>
        /// <param name="Metric">Core metric.</param>
        internal record MetricDetails(
            string Name, string? Unit, string? Description, Bridge.MetricInteger Metric)
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MetricDetails" /> class.
            /// </summary>
            /// <param name="meter">Core meter.</param>
            /// <param name="kind">Kind.</param>
            /// <param name="name">Name.</param>
            /// <param name="unit">Unit.</param>
            /// <param name="description">Description.</param>
            internal MetricDetails(
                Bridge.MetricMeter meter,
                Bridge.Interop.MetricIntegerKind kind,
                string name,
                string? unit,
                string? description)
                : this(
                    Name: name,
                    Unit: unit,
                    Description: description,
#pragma warning disable CA2000 // We are choosing to let finalizer dispose this
                    Metric: new(meter, kind, name, unit, description))
#pragma warning restore CA2000
            {
            }
        }
    }
}