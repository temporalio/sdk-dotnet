using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Representation of a payload converter for a specific encoding.
    /// </summary>
    public interface IEncodingConverter
    {
        /// <summary>
        /// The encoding name this converter represents.
        /// </summary>
        /// <remarks>
        /// Implementers must put this value on the "encoding" metadata of created payloads.
        /// </remarks>
        string Encoding { get; }

        /// <summary>
        /// Try to convert the given value to the given payload or return false if this converter
        /// cannot handle it and the next should be tried.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="payload">The payload to set.</param>
        /// <returns>True if this converter can and has converted the value.</returns>
        /// <remarks>
        /// Implementers must put the <see cref="Encoding" /> value on the "encoding" metadata of
        /// created payloads.
        /// </remarks>
        bool TryToPayload(object? value, out Payload? payload);

        /// <summary>
        /// Convert the given payload to the given type or error.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="payload">The payload to convert from.</param>
        /// <returns>The converted value.</returns>
        /// <remarks>
        /// This call is guaranteed to only be called for payloads whose metadata match
        /// <see cref="Encoding" />. This should error if it cannot convert to the given type.
        /// </remarks>
        T? ToValue<T>(Payload payload);
    }
}
