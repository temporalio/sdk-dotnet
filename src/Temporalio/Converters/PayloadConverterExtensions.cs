using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Extensions for data, payload, and encoding converters.
    /// </summary>
    public static class ConverterExtensions
    {
        /// <summary>
        /// Convert the given payload to a value of the given type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="converter">The converter to use.</param>
        /// <param name="payload">The payload to convert.</param>
        /// <returns>The converted value.</returns>
        public static T? ToValue<T>(this IPayloadConverter converter, Payload payload)
        {
            return (T?)converter.ToValue(payload, typeof(T));
        }
    }
}
