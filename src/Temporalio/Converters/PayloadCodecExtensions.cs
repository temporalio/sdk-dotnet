using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Failure.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Extension methods for <see cref="IPayloadCodec" />.
    /// </summary>
    public static class PayloadCodecExtensions
    {
        /// <summary>
        /// Single-value form of <see cref="IPayloadCodec.EncodeAsync" />. The multi-value form
        /// should be used in multi-value situations since the output arity can change compared to
        /// input. This is only for cases where there is only ever one payload.
        /// </summary>
        /// <param name="codec">Codec to use.</param>
        /// <param name="payload">Payload to encode.</param>
        /// <returns>Encoded payload.</returns>
        public static async Task<Payload> EncodeSingleAsync(
            this IPayloadCodec codec, Payload payload) =>
            (await codec.EncodeAsync(new[] { payload }).ConfigureAwait(false)).Single();

        /// <summary>
        /// Single-value form of <see cref="IPayloadCodec.DecodeAsync" />. The multi-value form
        /// should be used in multi-value situations since the output arity can change compared to
        /// input. This is only for cases where there is only ever one payload.
        /// </summary>
        /// <param name="codec">Codec to use.</param>
        /// <param name="payload">Payload to decode.</param>
        /// <returns>Decoded payload.</returns>
        public static async Task<Payload> DecodeSingleAsync(
            this IPayloadCodec codec, Payload payload) =>
            (await codec.DecodeAsync(new[] { payload }).ConfigureAwait(false)).Single();

        /// <summary>
        /// Encode all payloads in the given failure in place.
        /// </summary>
        /// <param name="codec">Codec to use.</param>
        /// <param name="failure">Failure to encode.</param>
        /// <returns>Task for completion.</returns>
        public static Task EncodeFailureAsync(this IPayloadCodec codec, Failure failure) =>
            ApplyToFailurePayloadAsync(failure, codec.EncodeAsync);

        /// <summary>
        /// Decode all payloads in the given failure in place.
        /// </summary>
        /// <param name="codec">Codec to use.</param>
        /// <param name="failure">Failure to decode.</param>
        /// <returns>Task for completion.</returns>
        public static Task DecodeFailureAsync(this IPayloadCodec codec, Failure failure) =>
            ApplyToFailurePayloadAsync(failure, codec.DecodeAsync);

        private static async Task ApplyToFailurePayloadAsync(
            Failure failure,
            Func<IReadOnlyCollection<Payload>, Task<IReadOnlyCollection<Payload>>> func)
        {
            if (failure.EncodedAttributes != null)
            {
                failure.EncodedAttributes = (await func.Invoke(
                    new Payload[] { failure.EncodedAttributes }).ConfigureAwait(false)).First();
            }
            switch (failure.FailureInfoCase)
            {
                case Failure.FailureInfoOneofCase.ApplicationFailureInfo:
                    await ApplyPayloadsAsync(
                        failure.ApplicationFailureInfo.Details, func).ConfigureAwait(false);
                    break;
                case Failure.FailureInfoOneofCase.TimeoutFailureInfo:
                    await ApplyPayloadsAsync(
                        failure.TimeoutFailureInfo.LastHeartbeatDetails, func).ConfigureAwait(false);
                    break;
                case Failure.FailureInfoOneofCase.CanceledFailureInfo:
                    await ApplyPayloadsAsync(
                        failure.CanceledFailureInfo.Details, func).ConfigureAwait(false);
                    break;
                case Failure.FailureInfoOneofCase.ResetWorkflowFailureInfo:
                    await ApplyPayloadsAsync(
                        failure.ResetWorkflowFailureInfo.LastHeartbeatDetails,
                        func).ConfigureAwait(false);
                    break;
            }
            if (failure.Cause != null)
            {
                await ApplyToFailurePayloadAsync(failure.Cause, func).ConfigureAwait(false);
            }
        }

        private static async Task ApplyPayloadsAsync(
            Payloads payloads,
            Func<IReadOnlyCollection<Payload>, Task<IReadOnlyCollection<Payload>>> func)
        {
            if (payloads != null && payloads.Payloads_.Count > 0)
            {
                // We have to convert to list just in case this is a lazy enumerable
                var newPayloads = (await func.Invoke(
                    payloads.Payloads_).ConfigureAwait(false)).ToList();
                payloads.Payloads_.Clear();
                payloads.Payloads_.Add(newPayloads);
            }
        }
    }
}
