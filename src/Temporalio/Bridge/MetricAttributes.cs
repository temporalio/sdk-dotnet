using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core-owned metric attributes.
    /// </summary>
    internal class MetricAttributes : SafeHandle
    {
        private readonly MetricMeter meter;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricAttributes"/> class.
        /// </summary>
        /// <param name="meter">Core meter.</param>
        /// <param name="attributes">Full attribute set.</param>
        public MetricAttributes(MetricMeter meter, IEnumerable<KeyValuePair<string, object>> attributes)
            : base(IntPtr.Zero, true)
        {
            this.meter = meter;
            using (var scope = new Scope())
            {
                unsafe
                {
                    var attrs = ConvertKeyValuePairs(scope, attributes);
                    Ptr = Interop.Methods.metric_attributes_new(
                        meter.Ptr, scope.ArrayPointer(attrs), (UIntPtr)attrs.Length);
                    SetHandle((IntPtr)Ptr);
                }
            }
        }

        private unsafe MetricAttributes(MetricMeter meter, Interop.MetricAttributes* ptr)
            : base((IntPtr)ptr, true)
        {
            this.meter = meter;
            unsafe
            {
                Ptr = ptr;
            }
        }

        /// <inheritdoc />
        public override unsafe bool IsInvalid => false;

        /// <summary>
        /// Gets the pointer to the attributes.
        /// </summary>
        internal unsafe Interop.MetricAttributes* Ptr { get; private init; }

        /// <summary>
        /// Create a new instance with the given attributes appended.
        /// </summary>
        /// <param name="attributes">Attributes to append.</param>
        /// <returns>New attributes with the given ones appended.</returns>
        public MetricAttributes Append(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            using (var scope = new Scope())
            {
                unsafe
                {
                    var attrs = ConvertKeyValuePairs(scope, attributes);
                    var newAttrs = Interop.Methods.metric_attributes_new_append(
                        meter.Ptr, Ptr, scope.ArrayPointer(attrs), (UIntPtr)attrs.Length);
                    return new(meter, newAttrs);
                }
            }
        }

        /// <inheritdoc />
        protected override unsafe bool ReleaseHandle()
        {
            Interop.Methods.metric_attributes_free(Ptr);
            return true;
        }

        private static Interop.MetricAttribute[] ConvertKeyValuePairs(
            Scope scope, IEnumerable<KeyValuePair<string, object>> attributes) =>
            attributes.Select(pair => ConvertKeyValuePair(scope, pair)).ToArray();

        private static Interop.MetricAttribute ConvertKeyValuePair(
            Scope scope, KeyValuePair<string, object> pair)
        {
            var key = scope.ByteArray(pair.Key);
            switch (pair.Value)
            {
                case int value:
                    return new()
                    {
                        key = key,
                        value = new() { int_value = value },
                        value_type = Interop.MetricAttributeValueType.Int,
                    };
                case long value:
                    return new()
                    {
                        key = key,
                        value = new() { int_value = value },
                        value_type = Interop.MetricAttributeValueType.Int,
                    };
                case float value:
                    return new()
                    {
                        key = key,
                        value = new() { float_value = value },
                        value_type = Interop.MetricAttributeValueType.Float,
                    };
                case double value:
                    return new()
                    {
                        key = key,
                        value = new() { float_value = value },
                        value_type = Interop.MetricAttributeValueType.Float,
                    };
                case bool value:
                    return new()
                    {
                        key = key,
                        value = new() { bool_value = (byte)(value ? 1 : 0) },
                        value_type = Interop.MetricAttributeValueType.Bool,
                    };
                default:
                    return new()
                    {
                        key = key,
                        value = new() { string_value = scope.ByteArray(Convert.ToString(pair.Value)) },
                        value_type = Interop.MetricAttributeValueType.String,
                    };
            }
        }
    }
}
