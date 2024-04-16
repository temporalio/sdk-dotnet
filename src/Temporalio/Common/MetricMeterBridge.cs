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
            if (!IsInteger<T>())
            {
                throw new ArgumentException($"Invalid metric type of {typeof(T)}, must be integer");
            }
            return new Counter<T>(
                new(name, unit, description),
                new(meter, Bridge.Interop.MetricKind.CounterInteger, name, unit, description),
                Bridge.Interop.MetricKind.CounterInteger,
                attributes);
        }

        /// <inheritdoc />
        public override MetricHistogram<T> CreateHistogram<T>(
            string name, string? unit = null, string? description = null)
            where T : struct
        {
            Bridge.Interop.MetricKind kind;
            if (IsInteger<T>())
            {
                kind = Bridge.Interop.MetricKind.HistogramInteger;
            }
            else if (IsFloat<T>())
            {
                kind = Bridge.Interop.MetricKind.HistogramFloat;
            }
            else if (typeof(T) == typeof(TimeSpan))
            {
                kind = Bridge.Interop.MetricKind.HistogramDuration;
            }
            else
            {
                throw new ArgumentException($"Invalid metric type of {typeof(T)}, must be integer, float, or TimeSpan");
            }
            return new Histogram<T>(
                new(name, unit, description),
                new(meter, kind, name, unit, description),
                kind,
                attributes);
        }

        /// <inheritdoc />
        public override MetricGauge<T> CreateGauge<T>(
            string name, string? unit = null, string? description = null)
            where T : struct
        {
            Bridge.Interop.MetricKind kind;
            if (IsInteger<T>())
            {
                kind = Bridge.Interop.MetricKind.GaugeInteger;
            }
            else if (IsFloat<T>())
            {
                kind = Bridge.Interop.MetricKind.GaugeFloat;
            }
            else
            {
                throw new ArgumentException($"Invalid metric type of {typeof(T)}, must be integer or float");
            }
            return new Gauge<T>(
                new(name, unit, description),
                new(meter, kind, name, unit, description),
                kind,
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
        {
            // If there is not one on the bridge runtime, use a noop
            if (runtime.MetricMeter.Value is { } bridgeMeter)
            {
                return new MetricMeterBridge(bridgeMeter, bridgeMeter.DefaultAttributes);
            }
            return MetricMeterNoop.Instance;
        });

        private static bool IsInteger<T>()
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
                    return true;
            }
            return false;
        }

        private static ulong GetIntegerValue<T>(T value)
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

        private static void RecordInteger<T>(
            Bridge.Metric metric,
            Bridge.MetricAttributes attributes,
            T value,
            IEnumerable<KeyValuePair<string, object>>? extraTags)
            where T : struct
        {
            var ulongValue = GetIntegerValue(value);
            if (extraTags is { } extraAttrs)
            {
                using (var withExtras = attributes.Append(extraAttrs))
                {
                    metric.RecordInteger(ulongValue, withExtras);
                }
            }
            else
            {
                metric.RecordInteger(ulongValue, attributes);
            }
        }

        private static bool IsFloat<T>()
        {
            switch (Type.GetTypeCode(typeof(T)))
            {
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
            }
            return false;
        }

        private static double GetFloatValue<T>(T value)
            where T : struct
        {
            switch (value)
            {
                case float v:
                    if (v < 0)
                    {
                        throw new ArgumentException("Value cannot be negative");
                    }
                    return (double)v;
                case double v:
                    if (v < 0)
                    {
                        throw new ArgumentException("Value cannot be negative");
                    }
                    return (double)v;
                case decimal v:
                    if (v < 0)
                    {
                        throw new ArgumentException("Value cannot be negative");
                    }
                    return decimal.ToDouble(v);
                default:
                    throw new ArgumentException($"Only float metric types supported, but got {value.GetType()}");
            }
        }

        private static void RecordFloat<T>(
            Bridge.Metric metric,
            Bridge.MetricAttributes attributes,
            T value,
            IEnumerable<KeyValuePair<string, object>>? extraTags)
            where T : struct
        {
            var floatValue = GetFloatValue(value);
            if (extraTags is { } extraAttrs)
            {
                using (var withExtras = attributes.Append(extraAttrs))
                {
                    metric.RecordFloat(floatValue, withExtras);
                }
            }
            else
            {
                metric.RecordFloat(floatValue, attributes);
            }
        }

        private static void RecordDuration<T>(
            Bridge.Metric metric,
            Bridge.MetricAttributes attributes,
            T value,
            IEnumerable<KeyValuePair<string, object>>? extraTags)
            where T : struct
        {
            var ms = value is TimeSpan v ? v.TotalMilliseconds :
                throw new ArgumentException($"Only TimeSpan types supported, but got {value.GetType()}");
            if (ms < 0)
            {
                throw new ArgumentException("Value cannot be negative");
            }
            if (extraTags is { } extraAttrs)
            {
                using (var withExtras = attributes.Append(extraAttrs))
                {
                    metric.RecordDuration((ulong)ms, withExtras);
                }
            }
            else
            {
                metric.RecordDuration((ulong)ms, attributes);
            }
        }

        private class Counter<T> : MetricCounter<T>
            where T : struct
        {
            private readonly Bridge.Metric metric;
            private readonly Bridge.Interop.MetricKind kind;
            private readonly Bridge.MetricAttributes attributes;

            internal Counter(
                MetricDetails details,
                Bridge.Metric metric,
                Bridge.Interop.MetricKind kind,
                Bridge.MetricAttributes attributes)
                : base(details)
            {
                this.metric = metric;
                this.kind = kind;
                this.attributes = attributes;
            }

            public override void Add(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                RecordInteger(metric, attributes, value, extraTags);
            }

            public override MetricCounter<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Counter<T>(Details, metric, kind, attributes.Append(tags));
        }

        private class Histogram<T> : MetricHistogram<T>
            where T : struct
        {
            private readonly Bridge.Metric metric;
            private readonly Bridge.Interop.MetricKind kind;
            private readonly Bridge.MetricAttributes attributes;

            internal Histogram(
                MetricDetails details,
                Bridge.Metric metric,
                Bridge.Interop.MetricKind kind,
                Bridge.MetricAttributes attributes)
                : base(details)
            {
                this.metric = metric;
                this.kind = kind;
                this.attributes = attributes;
            }

            public override void Record(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                switch (kind)
                {
                    case Bridge.Interop.MetricKind.HistogramInteger:
                        RecordInteger(metric, attributes, value, extraTags);
                        break;
                    case Bridge.Interop.MetricKind.HistogramFloat:
                        RecordFloat(metric, attributes, value, extraTags);
                        break;
                    case Bridge.Interop.MetricKind.HistogramDuration:
                        RecordDuration(metric, attributes, value, extraTags);
                        break;
                }
            }

            public override MetricHistogram<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Histogram<T>(Details, metric, kind, attributes.Append(tags));
        }

        private class Gauge<T> : MetricGauge<T>
            where T : struct
        {
            private readonly Bridge.Metric metric;
            private readonly Bridge.Interop.MetricKind kind;
            private readonly Bridge.MetricAttributes attributes;

            internal Gauge(
                MetricDetails details,
                Bridge.Metric metric,
                Bridge.Interop.MetricKind kind,
                Bridge.MetricAttributes attributes)
                : base(details)
            {
                this.metric = metric;
                this.kind = kind;
                this.attributes = attributes;
            }

#pragma warning disable CA1716 // We are ok with using the "Set" name even though "set" is in lang
            public override void Set(
                T value, IEnumerable<KeyValuePair<string, object>>? extraTags = null)
            {
                switch (kind)
                {
                    case Bridge.Interop.MetricKind.GaugeInteger:
                        RecordInteger(metric, attributes, value, extraTags);
                        break;
                    case Bridge.Interop.MetricKind.GaugeFloat:
                        RecordFloat(metric, attributes, value, extraTags);
                        break;
                }
            }
#pragma warning restore CA1716

            public override MetricGauge<T> WithTags(IEnumerable<KeyValuePair<string, object>> tags) =>
                new Gauge<T>(Details, metric, kind, attributes.Append(tags));
        }
    }
}