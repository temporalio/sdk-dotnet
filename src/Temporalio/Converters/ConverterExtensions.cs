using System;
using Temporalio.Api.Common.V1;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Extensions for data, payload, and encoding converters.
    /// </summary>
    public static class ConverterExtensions
    {
        public static async Task<IEnumerable<Payload>> ToPayloadsAsync(this DataConverter converter, IReadOnlyCollection<object?> values)
        {
            // Convert then encode
            var payloads = values.Select(converter.PayloadConverter.ToPayload);
            if (converter.PayloadCodec != null) {
                // TODO(cretz): Ok if I'm lazy and use Linq knowing that ToList may cost
                payloads = await converter.PayloadCodec.EncodeAsync(payloads.ToList());
            }
            return payloads;
        }

        public static async Task<Payload> ToPayloadAsync(this DataConverter converter, object? value)
        {
            // Convert then encode
            var payload = converter.PayloadConverter.ToPayload(value);
            if (converter.PayloadCodec != null) {
                payload = (await converter.PayloadCodec.EncodeAsync(new Payload[] { payload })).First();
            }
            return payload;
        }

        public static async Task<T> ToSingleValueAsync<T>(this DataConverter converter, IReadOnlyCollection<Payload> payloads)
        {
            // Decode, then expect single payload
            if (converter.PayloadCodec != null) {
                payloads = (await converter.PayloadCodec.DecodeAsync(payloads)).ToList();
            }
            if (payloads.Count != 1) {
                throw new ArgumentException($"Expected 1 payload, found {payloads.Count}");
            }
            return converter.PayloadConverter.ToValue<T>(payloads.First());
        }

        // TODO(cretz): Note that this mutates failure
        public static async Task<Exception> ToExceptionAsync(this DataConverter converter, Failure failure)
        {
            // Decode then convert
            if (converter.PayloadCodec != null) {
                await converter.PayloadCodec.DecodeFailureAsync(failure);
            }
            return converter.FailureConverter.ToException(failure, converter.PayloadConverter);
        }

        /// <summary>
        /// Convert the given payload to a value of the given type.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="converter">The converter to use.</param>
        /// <param name="payload">The payload to convert.</param>
        /// <returns>The converted value.</returns>
        public static T ToValue<T>(this IPayloadConverter converter, Payload payload)
        {
            // We count on the payload converter to check whether the type is nullable
            return (T)converter.ToValue(payload, typeof(T))!;
        }
    }
}
