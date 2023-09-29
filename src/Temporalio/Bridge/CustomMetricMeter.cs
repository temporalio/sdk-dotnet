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
        private readonly List<GCHandle> handles = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomMetricMeter" /> class.
        /// </summary>
        /// <param name="meter">Meter implementation.</param>
        public unsafe CustomMetricMeter(Temporalio.Runtime.ICustomMetricMeter meter)
        {
            this.meter = meter;

            // Create metric meter struct
            var interopMeter = new Interop.CustomMetricMeter()
            {
                metric_integer_new = FunctionPointer<Interop.CustomMetricMeterMetricIntegerNewCallback>(CreateMetric),
                metric_integer_free = FunctionPointer<Interop.CustomMetricMeterMetricIntegerFreeCallback>(FreeMetric),
                metric_integer_update = FunctionPointer<Interop.CustomMetricMeterMetricIntegerUpdateCallback>(UpdateMetric),
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
            Interop.MetricIntegerKind kind)
        {
            Temporalio.Runtime.ICustomMetric<long> metric;
            var nameStr = GetString(name);
            var unitStr = GetStringOrNull(unit);
            var descStr = GetStringOrNull(description);
            switch (kind)
            {
                case Interop.MetricIntegerKind.Counter:
                    metric = meter.CreateCounter<long>(nameStr, unitStr, descStr);
                    break;
                case Interop.MetricIntegerKind.Histogram:
                    metric = meter.CreateHistogram<long>(nameStr, unitStr, descStr);
                    break;
                case Interop.MetricIntegerKind.Gauge:
                    metric = meter.CreateCounter<long>(nameStr, unitStr, descStr);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown kind: {kind}");
            }
            // Return pointer
            return GCHandle.ToIntPtr(GCHandle.Alloc(metric)).ToPointer();
        }

        private unsafe void FreeMetric(void* metric) => GCHandle.FromIntPtr(new(metric)).Free();

        private unsafe void UpdateMetric(void* metric, ulong value, void* attributes)
        {
            var metricObject = (Temporalio.Runtime.ICustomMetric<long>)GCHandle.FromIntPtr(new(metric)).Target!;
            var tags = GCHandle.FromIntPtr(new(attributes)).Target!;
            // We trust that value will never be over Int64.MaxValue
            var metricValue = unchecked((long)value);
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