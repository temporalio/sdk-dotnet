using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned metric.
    /// </summary>
    internal class Metric : SafeHandle
    {
        private readonly unsafe Interop.Metric* ptr;

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
            Interop.MetricKind kind,
            string name,
            string? unit,
            string? description)
            : base(IntPtr.Zero, true)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    var options = new Interop.MetricOptions()
                    {
                        name = scope.ByteArray(name),
                        description = scope.ByteArray(description ?? string.Empty),
                        unit = scope.ByteArray(unit ?? string.Empty),
                        kind = kind,
                    };
                    ptr = Interop.Methods.metric_new(meter.Ptr, scope.Pointer(options));
                    SetHandle((IntPtr)ptr);
                }
            }
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Record a value for the metric.
        /// </summary>
        /// <param name="value">Value to record.</param>
        /// <param name="attributes">Attributes to set.</param>
        public void RecordInteger(ulong value, MetricAttributes attributes)
        {
            unsafe
            {
                Interop.Methods.metric_record_integer(ptr, value, attributes.Ptr);
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
                Interop.Methods.metric_record_float(ptr, value, attributes.Ptr);
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
                Interop.Methods.metric_record_duration(ptr, valueMs, attributes.Ptr);
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.metric_free(ptr);
            return true;
        }
    }
}
