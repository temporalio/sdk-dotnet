using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned metric meter.
    /// </summary>
    internal class MetricMeter : SafeHandle
    {
        private unsafe MetricMeter(Interop.MetricMeter* ptr)
            : base(IntPtr.Zero, true)
        {
            Ptr = ptr;
            SetHandle((IntPtr)Ptr);
            DefaultAttributes = new(this, Enumerable.Empty<KeyValuePair<string, object>>());
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => Ptr == null;

        /// <summary>
        /// Gets the default attribute set.
        /// </summary>
        public MetricAttributes DefaultAttributes { get; private init; }

        /// <summary>
        /// Gets the pointer to the meter.
        /// </summary>
        internal unsafe Interop.MetricMeter* Ptr { get; private init; }

        /// <summary>
        /// Create a new metric meter from runtime if any configured.
        /// </summary>
        /// <param name="runtime">Runtime.</param>
        /// <returns>Meter or null if none configured.</returns>
        public static MetricMeter? CreateFromRuntime(Runtime runtime)
        {
            unsafe
            {
                var ptr = Interop.Methods.metric_meter_new(runtime.Ptr);
                if (ptr == null)
                {
                    return null;
                }
                return new(ptr);
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.metric_meter_free(Ptr);
            return true;
        }
    }
}
