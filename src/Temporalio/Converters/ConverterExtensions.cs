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
        /// Decode and convert the given payload to a value of the given type.
        /// </summary>
        /// <remarks>
        /// Usually <see cref="ToSingleValueAsync" /> is better because an encoder could have
        /// encoded into multiple payloads. However for some maps like memos and headers, there may
        /// only be a single payload to decode.
        /// </remarks>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="converter">The converter to use.</param>
        /// <param name="payload">The payload to convert.</param>
        /// <returns>Decoded and converted value.</returns>
        public static Task<T> ToValueAsync<T>(this DataConverter converter, Payload payload)
        {
            return converter.ToSingleValueAsync<T>(new Payload[] { payload });
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
        /// <param name="attrs">Attributes to convert.</param>
        /// <returns>Protobuf search attributes.</returns>
        public static SearchAttributes ToSearchAttributesProto(
            this IReadOnlyCollection<KeyValuePair<string, object>> attrs)
        {
            return new()
            {
                IndexedFields =
                {
                    attrs.ToDictionary(
                        pair => pair.Key,
                        pair =>
                        {
                            try
                            {
                                return DataConverter.Default.PayloadConverter.ToSearchAttributePayload(pair.Value);
                            }
                            catch (ArgumentException e)
                            {
                                throw new ArgumentException($"Failed converting search attribute {pair.Key}", e);
                            }
                        }),
                },
            };
        }

        /// <summary>
        /// Use the default payload converter to convert payloads to search attribute values.
        /// </summary>
        /// <param name="payloads">Payloads to convert.</param>
        /// <returns>Converted values.</returns>
        /// <exception cref="ArgumentException">
        /// If any value isn't a DateTime, IEnumerable&lt;string&gt;, or primitive.
        /// </exception>
        public static IDictionary<string, object> ToSearchAttributeValues(
            this SearchAttributes payloads)
        {
            return payloads.IndexedFields.ToSearchAttributeValues();
        }

        /// <summary>
        /// Use the default payload converter to convert payloads to search attribute values.
        /// </summary>
        /// <param name="payloads">Payloads to convert.</param>
        /// <returns>Converted values.</returns>
        public static IDictionary<string, object> ToSearchAttributeValues(
            this IEnumerable<KeyValuePair<string, Payload>> payloads)
        {
            return payloads.ToDictionary(
                pair => pair.Key,
                pair =>
                {
                    try
                    {
                        return DataConverter.Default.PayloadConverter.ToSearchAttributeValue(pair.Value);
                    }
                    catch (ArgumentException e)
                    {
                        throw new ArgumentException($"Failed converting search attribute {pair.Key}", e);
                    }
                });
        }
    }
}
