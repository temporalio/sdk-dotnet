using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Converter for a type marked with <see cref="TemporalDataModelAttribute"/>.
    /// </summary>
    public interface ITemporalDataModelConverter
    {
        /// <summary>
        /// Gets the data-model type handed to or read from the payload converter.
        /// </summary>
        Type DataModelType { get; }

        /// <summary>
        /// Convert a value to the data-model value that should be passed to the payload converter.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="payloadConverter">Payload converter to use for nested payload fields.</param>
        /// <returns>Data-model value to pass to the payload converter.</returns>
        object? ToDataModel(object? value, IPayloadConverter payloadConverter);

        /// <summary>
        /// Convert a data-model value from the payload converter to the marked type.
        /// </summary>
        /// <param name="dataModel">Data-model value returned by the payload converter.</param>
        /// <param name="payloadConverter">Payload converter to use for nested payload fields.</param>
        /// <returns>Converted value.</returns>
        object? FromDataModel(object? dataModel, IPayloadConverter payloadConverter);
    }
}
