using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Representation of a converter from a value to/from a payload.
    /// </summary>
    /// <remarks>
    /// This converter should be deterministic since it is used for workflows. For the same reason,
    /// this converter should be immediate and avoid any network calls or any asynchronous/slow code
    /// paths.
    /// </remarks>
    /// <seealso cref="PayloadConverter" />
    public interface IPayloadConverter
    {
        /// <summary>
        /// Convert the given value to a payload.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted payload.</returns>
        Payload ToPayload(object? value);

        /// <summary>
        /// Conver the given payload to a value of the given type.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="payload">The payload to convert.</param>
        /// <returns>The converted value.</returns>
        T? ToValue<T>(Payload payload);
    }
}
