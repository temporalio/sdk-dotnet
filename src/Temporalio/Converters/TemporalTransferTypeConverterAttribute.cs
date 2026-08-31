using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Marks a type as converting to and from a transfer type before payload conversion.
    /// </summary>
    /// <remarks>
    /// This is used by the SDK payload converter to delegate hook-aware values to the configured
    /// payload converter using the transfer type representation provided by
    /// <see cref="ITemporalTransferTypeConverter"/>.
    /// A constructed generic type may specify a generic converter type definition. The SDK
    /// closes the converter with all generic arguments from the marked type in declaration order.
    /// The marked type and converter must have the same generic arity, and the arguments must
    /// satisfy the converter's generic constraints. Closed converter types remain supported for
    /// both generic and non-generic marked types.
    /// This API is experimental and may change in a future release.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class TemporalTransferTypeConverterAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="TemporalTransferTypeConverterAttribute"/> class.
        /// </summary>
        /// <param name="converterType">Closed converter type implementing
        /// <see cref="ITemporalTransferTypeConverter"/>, or a generic converter type
        /// definition whose generic parameters directly match those of the marked type.</param>
        public TemporalTransferTypeConverterAttribute(Type converterType) =>
            ConverterType = converterType;

        /// <summary>
        /// Gets the converter type.
        /// </summary>
        public Type ConverterType { get; }
    }
}
