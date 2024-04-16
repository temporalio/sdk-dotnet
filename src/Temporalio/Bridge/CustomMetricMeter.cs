using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core wrapper for a custom metric meter implementation.
    /// </summary>
    internal class CustomMetricMeter
    {
        private readonly Temporalio.Runtime.ICustomMetricMeter meter;
        private readonly Temporalio.Runtime.CustomMetricMeterOptions options;
        private readonly List<GCHandle> handles = new();

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
            var interopMeter = new Interop.CustomMetricMeter()
            {
                metric_new = FunctionPointer<Interop.CustomMetricMeterMetricNewCallback>(CreateMetric),
                metric_free = FunctionPointer<Interop.CustomMetricMeterMetricFreeCallback>(FreeMetric),
                metric_record_integer = FunctionPointer<Interop.CustomMetricMeterMetricRecordIntegerCallback>(RecordMetricInteger),
                metric_record_float = FunctionPointer<Interop.CustomMetricMeterMetricRecordFloatCallback>(RecordMetricFloat),
                metric_record_duration = FunctionPointer<Interop.CustomMetricMeterMetricRecordDurationCallback>(RecordMetricDuration),
                attributes_new = FunctionPointer<Interop.CustomMetricMeterAttributesNewCallback>(CreateAttributes),
                attributes_free = FunctionPointer<Interop.CustomMetricMeterAttributesFreeCallback>(FreeAttributes),
                meter_free = FunctionPointer<Interop.CustomMetricMeterMeterFreeCallback>(Free),
            };

            // Pin the metric meter pointer and set it as the first handle
            var interopMeterHandle = GCHandle.Alloc(interopMeter, GCHandleType.Pinned);
            handles.Insert(0, interopMeterHandle);
            Ptr = (Interop.CustomMetricMeter*)interopMeterHandle.AddrOfPinnedObject();

            // Add handle for ourself
            handles.Add(GCHandle.Alloc(this));
        }

        /// <summary>
        /// Gets the pointer to the native metric meter.
        /// </summary>
        internal unsafe Interop.CustomMetricMeter* Ptr { get; private init; }

        private static unsafe string? GetStringOrNull(Interop.ByteArrayRef bytes) =>
            (int)bytes.size == 0 ? null : GetString(bytes);

        private static unsafe string GetString(Interop.ByteArrayRef bytes) =>
            GetString(bytes.data, bytes.size);

        private static unsafe string GetString(byte* bytes, UIntPtr size) =>
            (int)size == 0 ? string.Empty : ByteArrayRef.StrictUTF8.GetString(bytes, (int)size);

        private unsafe void* CreateMetric(
            Interop.ByteArrayRef name,
            Interop.ByteArrayRef description,
            Interop.ByteArrayRef unit,
            Interop.MetricKind kind)
        {
            GCHandle metric;
            var nameStr = GetString(name);
            var unitStr = GetStringOrNull(unit);
            var descStr = GetStringOrNull(description);
            switch (kind)
            {
                case Interop.MetricKind.CounterInteger:
                    metric = GCHandle.Alloc(meter.CreateCounter<long>(nameStr, unitStr, descStr));
                    break;
                case Interop.MetricKind.HistogramInteger:
                    metric = GCHandle.Alloc(meter.CreateHistogram<long>(nameStr, unitStr, descStr));
                    break;
                case Interop.MetricKind.HistogramFloat:
                    metric = GCHandle.Alloc(meter.CreateHistogram<double>(nameStr, unitStr, descStr));
                    break;
                case Interop.MetricKind.HistogramDuration:
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
                case Interop.MetricKind.GaugeInteger:
                    metric = GCHandle.Alloc(meter.CreateGauge<long>(nameStr, unitStr, descStr));
                    break;
                case Interop.MetricKind.GaugeFloat:
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
            void* appendFrom, Interop.CustomMetricAttribute* attributes, UIntPtr attributesSize)
        {
            var appendFromObject = appendFrom == null ? null : GCHandle.FromIntPtr(new(appendFrom)).Target;
            var tags = new KeyValuePair<string, object>[(int)attributesSize];
            for (int i = 0; i < (int)attributesSize; i++)
            {
                var attribute = attributes[i];
                var key = GetString(attribute.key);
                switch (attribute.value_type)
                {
                    case Interop.MetricAttributeValueType.String:
                        tags[i] = new(key, GetString(attribute.value.string_value.data, attribute.value.string_value.size));
                        break;
                    case Interop.MetricAttributeValueType.Int:
                        tags[i] = new(key, attribute.value.int_value);
                        break;
                    case Interop.MetricAttributeValueType.Float:
                        tags[i] = new(key, attribute.value.float_value);
                        break;
                    case Interop.MetricAttributeValueType.Bool:
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

        private unsafe void Free(Interop.CustomMetricMeter* meter)
        {
            // Free in order which frees function pointers first then meter handles
            foreach (var handle in handles)
            {
                handle.Free();
            }
        }

        // Similar to Scope.FunctionPointer
        private IntPtr FunctionPointer<T>(T func)
            where T : Delegate
        {
            var handle = GCHandle.Alloc(func);
            handles.Add(handle);
            return Marshal.GetFunctionPointerForDelegate(handle.Target!);
        }
    }
}