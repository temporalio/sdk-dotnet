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
        /// <summary>
        /// Initializes a new instance of the <see cref="MetricMeter"/> class.
        /// </summary>
        /// <param name="runtime">Runtime.</param>
        public MetricMeter(Runtime runtime)
            : base(IntPtr.Zero, true)
        {
            unsafe
            {
                Ptr = Interop.Methods.metric_meter_new(runtime.Ptr);
                SetHandle((IntPtr)Ptr);
            }
            DefaultAttributes = new(this, Enumerable.Empty<KeyValuePair<string, object>>());
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Gets the default attribute set.
        /// </summary>
        public MetricAttributes DefaultAttributes { get; private init; }

        /// <summary>
        /// Gets the pointer to the meter.
        /// </summary>
        internal unsafe Interop.MetricMeter* Ptr { get; private init; }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.metric_meter_free(Ptr);
            return true;
        }
    }
}
