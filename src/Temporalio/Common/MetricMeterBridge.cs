using System;
using System.Collections.Generic;

namespace Temporalio.Common
{
    /// <inheritdoc />
    internal class MetricMeterBridge : MetricMeter
    {
        private readonly Bridge.MetricMeter meter;
        private readonly Bridge.MetricAttributes attributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMeterBridge" /> class.
        /// </summary>
        /// <param name="meter">Core meter.</param>
        /// <param name="attributes">Core attributes.</param>
        internal MetricMeterBridge(Bridge.MetricMeter meter, Bridge.MetricAttributes attributes)
        {
            this.meter = meter;
            this.attributes = attributes;
        }

        /// <inheritdoc />
        public override MetricCounter<T> CreateCounter<T>(
            string name, string? unit = null, string? description = null)
            where T : struct
        {
            AssertValidMetricType<T>();
            return new Counter<T>(
                new(name, unit, description),
                new(meter, Bridge.Interop.MetricIntegerKind.Counter, name, unit, description),
                attributes);
        }

        /// <inheritdoc />
        public override MetricHistogram<T> CreateHistogram<T>(
            string name, string? unit = null, string? description = null)
            where T : struct
        {
            AssertValidMetricType<T>();
            return new Histogram<T>(
                new(name, unit, description),
                new(meter, Bridge.Interop.MetricIntegerKind.Histogram, name, unit, description),
                attributes);
        }

        /// <inheritdoc />
        public override MetricGauge<T> CreateGauge<T>(
            string name, string? unit = null, string? description = null)
            where T : struct
        {
            AssertValidMetricType<T>();
            return new Gauge<T>(
                new(name, unit, description),
                new(meter, Bridge.Interop.MetricIntegerKind.Gauge, name, unit, description),
                attributes);
        }

        /// <inheritdoc />
        public override MetricMeter WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
            new MetricMeterBridge(meter, attributes.Append(tags));

        /// <summary>
        /// Create a new lazy meter creator.
        /// </summary>
        /// <param name="runtime">Runtime to base on.</param>
        /// <returns>Lazy meter.</returns>
        internal static Lazy<MetricMeter> LazyFromRuntime(Bridge.Runtime runtime) => new(() =>
            new MetricMeterBridge(runtime.MetricMeter.Value, runtime.MetricMeter.Value.DefaultAttributes));

        private static void AssertValidMetricType<T>()
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return;
            }
            throw new ArgumentException($"Metric type must be an integer numeric, got: {typeof(T)}");
        }

        private static ulong GetMetricValue<T>(T value)
            where T : struct
        {
            switch (value)
            {
                case sbyte v:
                    if (v < 0)
                    {
                        throw new ArgumentException("Value cannot be negative");
                    }
                    return (ulong)v;
                case short v:
                    if (v < 0)
                    {
                        throw new ArgumentException("Value cannot be negative");
                    }
                    return (ulong)v;
                case int v:
                    if (v < 0)
                    {
                        throw new ArgumentException("Value cannot be negative");
                    }
                    return (ulong)v;
                case long v:
                    if (v < 0)
                    {
                        throw new ArgumentException("Value cannot be negative");
                    }
                    return (ulong)v;
                case byte v:
                    return v;
                case ushort v:
                    return v;
                case uint v:
                    return v;
                case ulong v:
                    return v;
                default:
                    throw new ArgumentException($"Only integer metric types supported, but got {value.GetType()}");
            }
        }

        private static void RecordMetric<T>(
            Bridge.MetricInteger metric,
            Bridge.MetricAttributes attributes,
            T value,
            IEnumerable<KeyValuePair<string, object>>? extraTags)
            where T : struct
        {
            var ulongValue = GetMetricValue(value);
            if (extraTags is { } extraAttrs)
            {
                using (var withExtras = attributes.Append(extraAttrs))
                {
                    metric.Record(ulongValue, withExtras);
                }
            }
            else
            {
                metric.Record(ulongValue, attributes);
            }
        }

        private class Counter<T> : MetricCounter<T>
            where T : struct
        {
            private readonly Bridge.MetricInteger metric;
            private readonly Bridge.MetricAttributes attributes;

            internal Counter(
                MetricDetails details,
                Bridge.MetricInteger metric,
                Bridge.MetricAttributes attributes)
                : base(details)
            {
                this.metric = metric;
                this.attributes = attributes;
            }

            public override void Add(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null) =>
                RecordMetric(metric, attributes, value, extraTags);

            public override MetricCounter<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Counter<T>(Details, metric, attributes.Append(tags));
        }

        private class Histogram<T> : MetricHistogram<T>
            where T : struct
        {
            private readonly Bridge.MetricInteger metric;
            private readonly Bridge.MetricAttributes attributes;

            internal Histogram(
                MetricDetails details,
                Bridge.MetricInteger metric,
                Bridge.MetricAttributes attributes)
                : base(details)
            {
                this.metric = metric;
                this.attributes = attributes;
            }

            public override void Record(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null) =>
                RecordMetric(metric, attributes, value, extraTags);

            public override MetricHistogram<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Histogram<T>(Details, metric, attributes.Append(tags));
        }

        private class Gauge<T> : MetricGauge<T>
            where T : struct
        {
            private readonly Bridge.MetricInteger metric;
            private readonly Bridge.MetricAttributes attributes;

            internal Gauge(
                MetricDetails details,
                Bridge.MetricInteger metric,
                Bridge.MetricAttributes attributes)
                : base(details)
            {
                this.metric = metric;
                this.attributes = attributes;
            }

#pragma warning disable CA1716 // We are ok with using the "Set" name even though "set" is in lang
            public override void Set(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null) =>
                RecordMetric(metric, attributes, value, extraTags);
#pragma warning restore CA1716

            public override MetricGauge<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Gauge<T>(Details, metric, attributes.Append(tags));
        }
    }
}