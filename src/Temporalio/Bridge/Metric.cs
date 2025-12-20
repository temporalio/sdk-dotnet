using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned metric.
    /// </summary>
    internal class Metric : SafeHandle
    {
        private unsafe Interop.TemporalCoreMetric* ptr;

        /// <summary>
        /// Initializes a new instance of the <see cref="Metric"/> class.
        /// </summary>
        /// <param name="meter">Core meter.</param>
        /// <param name="kind">Metric kind.</param>
        /// <param name="name">Metric name.</param>
        /// <param name="unit">Metric unit.</param>
        /// <param name="description">Metric description.</param>
        public Metric(
            MetricMeter meter,
            Interop.TemporalCoreMetricKind kind,
            string name,
            string? unit,
            string? description)
            : base(IntPtr.Zero, true)
        {
            Scope.WithScope(scope =>
            {
                unsafe
                {
                    var options = new Interop.TemporalCoreMetricOptions()
                    {
                        name = scope.ByteArray(name),
                        description = scope.ByteArray(description ?? string.Empty),
                        unit = scope.ByteArray(unit ?? string.Empty),
                        kind = kind,
                    };
                    ptr = Interop.Methods.temporal_core_metric_new(meter.Ptr, scope.Pointer(options));
                    SetHandle((IntPtr)ptr);
                }
            });
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => ptr == null;

        /// <summary>
        /// Record a value for the metric.
        /// </summary>
        /// <param name="value">Value to record.</param>
        /// <param name="attributes">Attributes to set.</param>
        public void RecordInteger(ulong value, MetricAttributes attributes)
        {
            unsafe
            {
                Interop.Methods.temporal_core_metric_record_integer(ptr, value, attributes.Ptr);
            }
        }

        /// <summary>
        /// Record a value for the metric.
        /// </summary>
        /// <param name="value">Value to record.</param>
        /// <param name="attributes">Attributes to set.</param>
        public void RecordFloat(double value, MetricAttributes attributes)
        {
            unsafe
            {
                Interop.Methods.temporal_core_metric_record_float(ptr, value, attributes.Ptr);
            }
        }

        /// <summary>
        /// Record a value for the metric.
        /// </summary>
        /// <param name="valueMs">Value to record.</param>
        /// <param name="attributes">Attributes to set.</param>
        public void RecordDuration(ulong valueMs, MetricAttributes attributes)
        {
            unsafe
            {
                Interop.Methods.temporal_core_metric_record_duration(ptr, valueMs, attributes.Ptr);
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.temporal_core_metric_free(ptr);
            return true;
        }
    }
}
