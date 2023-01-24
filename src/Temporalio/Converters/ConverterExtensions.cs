using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Extensions for data, payload, and encoding converters.
    /// </summary>
    public static class ConverterExtensions
    {
        /// <summary>
        /// Convert and encode the given values to payloads.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="values">Values to convert and encode.</param>
        /// <returns>Converted and encoded payloads.</returns>
        public static async Task<IEnumerable<Payload>> ToPayloadsAsync(this DataConverter converter, IReadOnlyCollection<object?> values)
        {
            // Convert then encode
            var payloads = values.Select(converter.PayloadConverter.ToPayload);
            if (converter.PayloadCodec != null)
            {
                // TODO(cretz): Ok if I'm lazy and use Linq knowing that ToList may cost
                payloads = await converter.PayloadCodec.EncodeAsync(payloads.ToList());
            }
            return payloads;
        }

        /// <summary>
        /// Convert and encode the given value to a payload.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="value">Value to convert and encode.</param>
        /// <returns>Converted and encoded payload.</returns>
        public static async Task<Payload> ToPayloadAsync(this DataConverter converter, object? value)
        {
            // Convert then encode
            var payload = converter.PayloadConverter.ToPayload(value);
            if (converter.PayloadCodec != null)
            {
                payload = (await converter.PayloadCodec.EncodeAsync(new Payload[] { payload })).First();
            }
            return payload;
        }

        /// <summary>
        /// Decode and convert the given payloads to a single value.
        /// </summary>
        /// <typeparam name="T">Type to convert to.</typeparam>
        /// <param name="converter">Converter to use.</param>
        /// <param name="payloads">Payloads to decode and convert.</param>
        /// <returns>Decoded and converted value.</returns>
        /// <exception cref="ArgumentException">If there is not exactly one value.</exception>
        public static async Task<T> ToSingleValueAsync<T>(this DataConverter converter, IReadOnlyCollection<Payload> payloads)
        {
            // Decode, then expect single payload
            if (converter.PayloadCodec != null)
            {
                payloads = (await converter.PayloadCodec.DecodeAsync(payloads)).ToList();
            }
            if (payloads.Count != 1)
            {
                throw new ArgumentException($"Expected 1 payload, found {payloads.Count}");
            }
            return converter.PayloadConverter.ToValue<T>(payloads.First());
        }

        /// <summary>
        /// Decode and convert the given failure to an exception. Note, the failure object may be
        /// mutated in place so callers should clone to avoid side effects.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="failure">Failure to convert and encode. This may be mutated.</param>
        /// <returns>Decoded and converted exception.</returns>
        public static async Task<Exception> ToExceptionAsync(this DataConverter converter, Failure failure)
        {
            // Decode then convert
            if (converter.PayloadCodec != null)
            {
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

        /// <summary>
        /// Use the default payload converter to convert attribute values to search attributes.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="attrs">Attributes to convert.</param>
        /// <returns>Protobuf search attributes.</returns>
        public static SearchAttributes ToSearchAttributesProto(
            this DefaultPayloadConverter converter,
            IReadOnlyCollection<KeyValuePair<string, object>> attrs)
        {
            return new()
            {
                IndexedFields =
                {
                    attrs.ToDictionary(
                        pair => pair.Key,
                        pair => converter.ToSearchAttributePayload(pair.Key, pair.Value)),
                },
            };
        }

        /// <summary>
        /// Convert a value to a search attribute or fail.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="key">Key, used for exception messages.</param>
        /// <param name="value">Value to convert.</param>
        /// <returns>Payload with the search attribute.</returns>
        /// <exception cref="ArgumentException">
        /// If the value isn't a DateTime, IEnumerable&lt;string&gt;, or primitive.
        /// </exception>
        public static Payload ToSearchAttributePayload(
            this DefaultPayloadConverter converter, string key, object value)
        {
            if (value == null)
            {
                throw new ArgumentException($"Search attribute {key} has null value");
            }
            if (value is DateTime dateTimeValue)
            {
                // 8601 with timezone
                value = dateTimeValue.ToString("o");
            }
            else if (value is not IEnumerable<string> && !value.GetType().IsPrimitive)
            {
                throw new ArgumentException(
                    $"Search attribute {key} must be DateTime, IEnumerable<string>, or primitive but is {value.GetType()}");
            }
            return converter.ToPayload(value);
        }
    }
}
