using System;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned metric for integers.
    /// </summary>
    internal class MetricInteger : SafeHandle
    {
        private readonly unsafe Interop.MetricInteger* ptr;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricInteger"/> class.
        /// </summary>
        /// <param name="meter">Core meter.</param>
        /// <param name="kind">Metric kind.</param>
        /// <param name="name">Metric name.</param>
        /// <param name="unit">Metric unit.</param>
        /// <param name="description">Metric description.</param>
        public MetricInteger(
            MetricMeter meter,
            Interop.MetricIntegerKind kind,
            string name,
            string? unit,
            string? description)
            : base(IntPtr.Zero, true)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    var options = new Interop.MetricIntegerOptions()
                    {
                        name = scope.ByteArray(name),
                        description = scope.ByteArray(description ?? string.Empty),
                        unit = scope.ByteArray(unit ?? string.Empty),
                        kind = kind,
                    };
                    ptr = Interop.Methods.metric_integer_new(meter.Ptr, scope.Pointer(options));
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
        public void Record(ulong value, MetricAttributes attributes)
        {
            unsafe
            {
                Interop.Methods.metric_integer_record(ptr, value, attributes.Ptr);
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.metric_integer_free(ptr);
            return true;
        }
    }
}
