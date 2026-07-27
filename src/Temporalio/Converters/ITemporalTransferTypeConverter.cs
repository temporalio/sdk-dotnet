using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Converter for a type marked with <see cref="TemporalTransferTypeConverterAttribute"/>.
    /// </summary>
    /// <remarks>
    /// This API is experimental and may change in a future release.
    /// </remarks>
    public interface ITemporalTransferTypeConverter
    {
        /// <summary>
        /// Gets the transfer type handed to or read from the payload converter.
        /// </summary>
        Type TransferType { get; }

        /// <summary>
        /// Convert a value to the transfer type value that should be passed to the payload converter.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <returns>Transfer type value to pass to the payload converter.</returns>
        object? ToTransferType(object? value);

        /// <summary>
        /// Convert a transfer type value from the payload converter to the marked type.
        /// </summary>
        /// <param name="transferType">Transfer type value returned by the payload converter.</param>
        /// <returns>Converted value.</returns>
        object? FromTransferType(object? transferType);
    }
}
