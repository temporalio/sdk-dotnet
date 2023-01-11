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
        /// Encode all payloads in the given failure in place.
        /// </summary>
        public static Task EncodeFailureAsync(this IPayloadCodec codec, Failure failure)
        {
            return ApplyToFailurePayloadAsync(failure, codec.EncodeAsync);
        }

        /// <summary>
        /// Decode all payloads in the given failure in place.
        /// </summary>
        public static Task DecodeFailureAsync(this IPayloadCodec codec, Failure failure)
        {
            return ApplyToFailurePayloadAsync(failure, codec.DecodeAsync);
        }

        private static async Task ApplyToFailurePayloadAsync(
            Failure failure,
            Func<IReadOnlyCollection<Payload>, Task<IEnumerable<Payload>>> func
        )
        {
            if (failure.EncodedAttributes != null)
            {
                failure.EncodedAttributes = (
                    await func.Invoke(new Payload[] { failure.EncodedAttributes })
                ).First();
            }
            switch (failure.FailureInfoCase)
            {
                case Failure.FailureInfoOneofCase.ApplicationFailureInfo:
                    await ApplyPayloadsAsync(failure.ApplicationFailureInfo.Details, func);
                    break;
                case Failure.FailureInfoOneofCase.TimeoutFailureInfo:
                    await ApplyPayloadsAsync(failure.TimeoutFailureInfo.LastHeartbeatDetails, func);
                    break;
                case Failure.FailureInfoOneofCase.CanceledFailureInfo:
                    await ApplyPayloadsAsync(failure.CanceledFailureInfo.Details, func);
                    break;
                case Failure.FailureInfoOneofCase.ResetWorkflowFailureInfo:
                    await ApplyPayloadsAsync(
                        failure.ResetWorkflowFailureInfo.LastHeartbeatDetails,
                        func
                    );
                    break;
            }
            if (failure.Cause != null)
            {
                await ApplyToFailurePayloadAsync(failure.Cause, func);
            }
        }

        private static async Task ApplyPayloadsAsync(
            Payloads payloads,
            Func<IReadOnlyCollection<Payload>, Task<IEnumerable<Payload>>> func
        )
        {
            if (payloads != null && payloads.Payloads_.Count > 0)
            {
                // We have to convert to list just in case this is a lazy enumerable
                var newPayloads = (await func.Invoke(payloads.Payloads_)).ToList();
                payloads.Payloads_.Clear();
                payloads.Payloads_.Add(newPayloads);
            }
        }
    }
}
