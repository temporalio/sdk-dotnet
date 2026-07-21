using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Marks a type as converting to and from another data-model type before payload conversion.
    /// </summary>
    /// <remarks>
    /// This is used by the SDK payload converter to delegate hook-aware values to the configured
    /// payload converter using the data-model representation provided by
    /// <see cref="ITemporalDataModelConverter"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
    public sealed class TemporalDataModelAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemporalDataModelAttribute"/> class.
        /// </summary>
        /// <param name="converterType">Converter type implementing
        /// <see cref="ITemporalDataModelConverter"/>.</param>
        public TemporalDataModelAttribute(Type converterType) => ConverterType = converterType;

        /// <summary>
        /// Gets the converter type.
        /// </summary>
        public Type ConverterType { get; }
    }
}
