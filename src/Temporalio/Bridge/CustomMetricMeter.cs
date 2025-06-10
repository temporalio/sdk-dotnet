using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core wrapper for a custom metric meter implementation.
    /// </summary>
    internal class CustomMetricMeter : NativeInvokeableClass<Interop.TemporalCoreCustomMetricMeter>
    {
        private readonly Temporalio.Runtime.ICustomMetricMeter meter;
        private readonly Temporalio.Runtime.CustomMetricMeterOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomMetricMeter" /> class.
        /// </summary>
        /// <param name="meter">Meter implementation.</param>
        /// <param name="options">Options.</param>
        public unsafe CustomMetricMeter(
            Temporalio.Runtime.ICustomMetricMeter meter,
            Temporalio.Runtime.CustomMetricMeterOptions options)
        {
            this.meter = meter;
            this.options = options;

            // Create metric meter struct
            var interopMeter = new Interop.TemporalCoreCustomMetricMeter()
            {
                metric_new = FunctionPointer<Interop.TemporalCoreCustomMetricMeterMetricNewCallback>(CreateMetric),
                metric_free = FunctionPointer<Interop.TemporalCoreCustomMetricMeterMetricFreeCallback>(FreeMetric),
                metric_record_integer = FunctionPointer<Interop.TemporalCoreCustomMetricMeterMetricRecordIntegerCallback>(RecordMetricInteger),
                metric_record_float = FunctionPointer<Interop.TemporalCoreCustomMetricMeterMetricRecordFloatCallback>(RecordMetricFloat),
                metric_record_duration = FunctionPointer<Interop.TemporalCoreCustomMetricMeterMetricRecordDurationCallback>(RecordMetricDuration),
                attributes_new = FunctionPointer<Interop.TemporalCoreCustomMetricMeterAttributesNewCallback>(CreateAttributes),
                attributes_free = FunctionPointer<Interop.TemporalCoreCustomMetricMeterAttributesFreeCallback>(FreeAttributes),
                meter_free = FunctionPointer<Interop.TemporalCoreCustomMetricMeterMeterFreeCallback>(Free),
            };

            PinCallbackHolder(interopMeter);
        }

        private static unsafe string? GetStringOrNull(Interop.TemporalCoreByteArrayRef bytes) =>
            (int)bytes.size == 0 ? null : GetString(bytes);

        private static unsafe string GetString(Interop.TemporalCoreByteArrayRef bytes) =>
            GetString(bytes.data, bytes.size);

        private static unsafe string GetString(byte* bytes, UIntPtr size) =>
            (int)size == 0 ? string.Empty : ByteArrayRef.StrictUTF8.GetString(bytes, (int)size);

        private unsafe void* CreateMetric(
            Interop.TemporalCoreByteArrayRef name,
            Interop.TemporalCoreByteArrayRef description,
            Interop.TemporalCoreByteArrayRef unit,
            Interop.TemporalCoreMetricKind kind)
        {
            GCHandle metric;
            var nameStr = GetString(name);
            var unitStr = GetStringOrNull(unit);
            var descStr = GetStringOrNull(description);
            switch (kind)
            {
                case Interop.TemporalCoreMetricKind.CounterInteger:
                    metric = GCHandle.Alloc(meter.CreateCounter<long>(nameStr, unitStr, descStr));
                    break;
                case Interop.TemporalCoreMetricKind.HistogramInteger:
                    metric = GCHandle.Alloc(meter.CreateHistogram<long>(nameStr, unitStr, descStr));
                    break;
                case Interop.TemporalCoreMetricKind.HistogramFloat:
                    metric = GCHandle.Alloc(meter.CreateHistogram<double>(nameStr, unitStr, descStr));
                    break;
                case Interop.TemporalCoreMetricKind.HistogramDuration:
                    switch (options.HistogramDurationFormat)
                    {
                        case Temporalio.Runtime.CustomMetricMeterOptions.DurationFormat.IntegerMilliseconds:
                            // Change unit from "duration" to "ms" since we're converting to ms
                            if (unitStr == "duration")
                            {
                                unitStr = "ms";
                            }
                            metric = GCHandle.Alloc(meter.CreateHistogram<long>(nameStr, unitStr, descStr));
                            break;
                        case Temporalio.Runtime.CustomMetricMeterOptions.DurationFormat.FloatSeconds:
                            // Change unit from "duration" to "s" since we're converting to s
                            if (unitStr == "duration")
                            {
                                unitStr = "s";
                            }
                            metric = GCHandle.Alloc(meter.CreateHistogram<double>(nameStr, unitStr, descStr));
                            break;
                        case Temporalio.Runtime.CustomMetricMeterOptions.DurationFormat.TimeSpan:
                            metric = GCHandle.Alloc(meter.CreateHistogram<TimeSpan>(nameStr, unitStr, descStr));
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown format: {options.HistogramDurationFormat}");
                    }
                    break;
                case Interop.TemporalCoreMetricKind.GaugeInteger:
                    metric = GCHandle.Alloc(meter.CreateGauge<long>(nameStr, unitStr, descStr));
                    break;
                case Interop.TemporalCoreMetricKind.GaugeFloat:
                    metric = GCHandle.Alloc(meter.CreateGauge<double>(nameStr, unitStr, descStr));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown kind: {kind}");
            }
            // Return pointer
            return GCHandle.ToIntPtr(metric).ToPointer();
        }

        private unsafe void FreeMetric(void* metric) => GCHandle.FromIntPtr(new(metric)).Free();

        private unsafe void RecordMetricInteger(void* metric, ulong value, void* attributes)
        {
            var metricObject = (Temporalio.Runtime.ICustomMetric<long>)GCHandle.FromIntPtr(new(metric)).Target!;
            var tags = GCHandle.FromIntPtr(new(attributes)).Target!;
            var metricValue = value > long.MaxValue ? long.MaxValue : unchecked((long)value);
            switch (metricObject)
            {
                case Temporalio.Runtime.ICustomMetricCounter<long> counter:
                    counter.Add(metricValue, tags);
                    break;
                case Temporalio.Runtime.ICustomMetricHistogram<long> histogram:
                    histogram.Record(metricValue, tags);
                    break;
                case Temporalio.Runtime.ICustomMetricGauge<long> gauge:
                    gauge.Set(metricValue, tags);
                    break;
            }
        }

        private unsafe void RecordMetricFloat(void* metric, double value, void* attributes)
        {
            var metricObject = (Temporalio.Runtime.ICustomMetric<double>)GCHandle.FromIntPtr(new(metric)).Target!;
            var tags = GCHandle.FromIntPtr(new(attributes)).Target!;
            switch (metricObject)
            {
                case Temporalio.Runtime.ICustomMetricHistogram<double> histogram:
                    histogram.Record(value, tags);
                    break;
                case Temporalio.Runtime.ICustomMetricGauge<double> gauge:
                    gauge.Set(value, tags);
                    break;
            }
        }

        private unsafe void RecordMetricDuration(void* metric, ulong valueMs, void* attributes)
        {
            var metricObject = GCHandle.FromIntPtr(new(metric)).Target!;
            var tags = GCHandle.FromIntPtr(new(attributes)).Target!;
            var metricValue = valueMs > long.MaxValue ? long.MaxValue : unchecked((long)valueMs);
            // We don't want to throw out of here, so we just fall through if anything doesn't match
            // expected types (which should never happen since we controlled creation)
            switch (options.HistogramDurationFormat)
            {
                case Temporalio.Runtime.CustomMetricMeterOptions.DurationFormat.IntegerMilliseconds:
                    if (metricObject is Temporalio.Runtime.ICustomMetricHistogram<long> histLong)
                    {
                        histLong.Record(metricValue, tags);
                    }
                    break;
                case Temporalio.Runtime.CustomMetricMeterOptions.DurationFormat.FloatSeconds:
                    if (metricObject is Temporalio.Runtime.ICustomMetricHistogram<double> histDouble)
                    {
                        histDouble.Record(metricValue / 1000.0, tags);
                    }
                    break;
                case Temporalio.Runtime.CustomMetricMeterOptions.DurationFormat.TimeSpan:
                    if (metricObject is Temporalio.Runtime.ICustomMetricHistogram<TimeSpan> histTimeSpan)
                    {
                        histTimeSpan.Record(TimeSpan.FromMilliseconds(metricValue), tags);
                    }
                    break;
            }
        }

        private unsafe void* CreateAttributes(
            void* appendFrom, Interop.TemporalCoreCustomMetricAttribute* attributes, UIntPtr attributesSize)
        {
            var appendFromObject = appendFrom == null ? null : GCHandle.FromIntPtr(new(appendFrom)).Target;
            var tags = new KeyValuePair<string, object>[(int)attributesSize];
            for (int i = 0; i < (int)attributesSize; i++)
            {
                var attribute = attributes[i];
                var key = GetString(attribute.key);
                switch (attribute.value_type)
                {
                    case Interop.TemporalCoreMetricAttributeValueType.String:
                        tags[i] = new(key, GetString(attribute.value.string_value.data, attribute.value.string_value.size));
                        break;
                    case Interop.TemporalCoreMetricAttributeValueType.Int:
                        tags[i] = new(key, attribute.value.int_value);
                        break;
                    case Interop.TemporalCoreMetricAttributeValueType.Float:
                        tags[i] = new(key, attribute.value.float_value);
                        break;
                    case Interop.TemporalCoreMetricAttributeValueType.Bool:
                        tags[i] = new(key, attribute.value.bool_value != 0);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown attribute type: {attribute.value_type}");
                }
            }
            var newAttributes = meter.CreateTags(appendFromObject, tags);
            // Return pointer
            return GCHandle.ToIntPtr(GCHandle.Alloc(newAttributes)).ToPointer();
        }

        private unsafe void FreeAttributes(void* attributes) => GCHandle.FromIntPtr(new(attributes)).Free();
    }
}
