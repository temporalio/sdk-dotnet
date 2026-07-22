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
    /// This API is experimental and may change in a future release.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
    public sealed class TemporalTransferTypeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalTransferTypeAttribute"/> class.
        /// </summary>
        /// <param name="converterType">Converter type implementing
        /// <see cref="ITemporalTransferTypeConverter"/>.</param>
        public TemporalTransferTypeAttribute(Type converterType) => ConverterType = converterType;

        /// <summary>
        /// Gets the converter type.
        /// </summary>
        public Type ConverterType { get; }
    }
}
