using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Failure.V1;
using Temporalio.Api.Sdk.V1;

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
        public static async Task<IReadOnlyCollection<Payload>> ToPayloadsAsync(
            this DataConverter converter, IReadOnlyCollection<object?> values)
        {
            // Convert then encode
            var payloads = values.Select(converter.PayloadConverter.ToPayload);
            if (converter.PayloadCodec != null)
            {
                // TODO(cretz): Ok if I'm lazy and use Linq knowing that ToList may cost
                payloads = await converter.PayloadCodec.EncodeAsync(
                    payloads.ToList()).ConfigureAwait(false);
            }
            return payloads.ToList();
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
                payload = (await converter.PayloadCodec.EncodeAsync(
                    new Payload[] { payload }).ConfigureAwait(false)).First();
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
        public static Task<T> ToValueAsync<T>(this DataConverter converter, Payload payload) =>
            converter.ToSingleValueAsync<T>(new Payload[] { payload });

        /// <summary>
        /// Decode and convert the given payload to a value of the given type.
        /// </summary>
        /// <remarks>
        /// Usually <see cref="ToSingleValueAsync" /> is better because an encoder could have
        /// encoded into multiple payloads. However for some maps like memos and headers, there may
        /// only be a single payload to decode.
        /// </remarks>
        /// <param name="converter">The converter to use.</param>
        /// <param name="payload">The payload to convert.</param>
        /// <param name="type">Type to convert to.</param>
        /// <returns>Decoded and converted value.</returns>
        public static Task<object?> ToValueAsync(
            this DataConverter converter, Payload payload, Type type) =>
            converter.ToSingleValueAsync(new Payload[] { payload }, type);

        /// <summary>
        /// Decode and convert the given payloads to a single value.
        /// </summary>
        /// <typeparam name="T">Type to convert to.</typeparam>
        /// <param name="converter">Converter to use.</param>
        /// <param name="payloads">Payloads to decode and convert.</param>
        /// <returns>Decoded and converted value.</returns>
        /// <exception cref="ArgumentException">If there is not exactly one value.</exception>
        public static async Task<T> ToSingleValueAsync<T>(
            this DataConverter converter, IReadOnlyCollection<Payload> payloads) =>
            (T)(await converter.ToSingleValueAsync(payloads, typeof(T)).ConfigureAwait(false))!;

        /// <summary>
        /// Decode and convert the given payloads to a single value.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="payloads">Payloads to decode and convert.</param>
        /// <param name="type">Type to convert to.</param>
        /// <returns>Decoded and converted value.</returns>
        /// <exception cref="ArgumentException">If there is not exactly one value.</exception>
        public static async Task<object?> ToSingleValueAsync(
            this DataConverter converter, IReadOnlyCollection<Payload> payloads, Type type)
        {
            // Decode, then expect single payload
            if (converter.PayloadCodec != null)
            {
                payloads = (await converter.PayloadCodec.DecodeAsync(
                    payloads).ConfigureAwait(false)).ToList();
            }
            if (payloads.Count != 1)
            {
                throw new ArgumentException($"Expected 1 payload, found {payloads.Count}");
            }
            return converter.PayloadConverter.ToValue(payloads.First(), type);
        }

        /// <summary>
        /// Decode and convert the given failure to an exception. Note, the failure object may be
        /// mutated in place so callers should clone to avoid side effects.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="failure">Failure to decode and convert. This may be mutated.</param>
        /// <returns>Decoded and converted exception.</returns>
        public static async Task<Exception> ToExceptionAsync(
            this DataConverter converter, Failure failure)
        {
            // Decode then convert
            if (converter.PayloadCodec != null)
            {
                await converter.PayloadCodec.DecodeFailureAsync(failure).ConfigureAwait(false);
            }
            return converter.FailureConverter.ToException(failure, converter.PayloadConverter);
        }

        /// <summary>
        /// Convert and encode the given exception to failure.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="exc">Exception convert and encode.</param>
        /// <returns>Converted and encoded failure.</returns>
        public static async Task<Failure> ToFailureAsync(
            this DataConverter converter, Exception exc)
        {
            // Convert then encode
            var failure = converter.FailureConverter.ToFailure(exc, converter.PayloadConverter);
            if (converter.PayloadCodec != null)
            {
                await converter.PayloadCodec.EncodeFailureAsync(failure).ConfigureAwait(false);
            }
            return failure;
        }

        /// <summary>
        /// Convert values to payloads only using the payload converter. Most users outside of a
        /// workflow should instead use the data converter to convert.
        /// </summary>
        /// <param name="converter">Converter to use.</param>
        /// <param name="values">Values to convert.</param>
        /// <returns>Converted values.</returns>
        public static IReadOnlyCollection<Payload> ToPayloads(
            this IPayloadConverter converter, IReadOnlyCollection<object?> values) =>
            values.Select(converter.ToPayload).ToList();

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
        /// Convert the given raw value to a value of the given type.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="converter">The converter to use.</param>
        /// <param name="rawValue">The raw value to convert.</param>
        /// <returns>The converted value.</returns>
        public static T ToValue<T>(this IPayloadConverter converter, IRawValue rawValue) =>
            converter.ToValue<T>(rawValue.Payload);

        /// <summary>
        /// Convert the given value to a raw value.
        /// </summary>
        /// <param name="converter">The converter to use.</param>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value.</returns>
        public static RawValue ToRawValue(this IPayloadConverter converter, object? value) =>
            new(converter.ToPayload(value));

        /// <summary>
        /// Create user metadata using this converter.
        /// </summary>
        /// <param name="converter">Converter.</param>
        /// <param name="summary">Summary.</param>
        /// <param name="details">Details.</param>
        /// <returns>Created metadata if any.</returns>
        internal static async Task<UserMetadata?> ToUserMetadataAsync(
            this DataConverter converter, string? summary, string? details)
        {
            if (summary == null && details == null)
            {
                return null;
            }
            var metadata = new UserMetadata();
            if (summary != null)
            {
                metadata.Summary = await converter.ToPayloadAsync(summary).ConfigureAwait(false);
            }
            if (details != null)
            {
                metadata.Details = await converter.ToPayloadAsync(details).ConfigureAwait(false);
            }
            return metadata;
        }

        /// <summary>
        /// Extract summary and details from the given user metadata.
        /// </summary>
        /// <param name="converter">Converter.</param>
        /// <param name="metadata">Metadata.</param>
        /// <returns>Extracted summary and details if any.</returns>
        internal static async Task<(string? Summary, string? Details)> FromUserMetadataAsync(
            this DataConverter converter, UserMetadata? metadata) => (
            Summary: metadata?.Summary is { } s ?
                await converter.ToValueAsync<string>(s).ConfigureAwait(false) : null,
            Details: metadata?.Details is { } d ?
                await converter.ToValueAsync<string>(d).ConfigureAwait(false) : null);
    }
}
