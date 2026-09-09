#pragma warning disable SA1600 // Internal implementation plumbing.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    internal static class PayloadCodecHelper
    {
        internal static async Task EncodeAsync(
            IPayloadCodec codec, RepeatedField<Payload> payloads)
        {
            if (payloads.Count == 0)
            {
                return;
            }
            var newPayloads = new List<Payload>();
            var codecPayloads = new List<Payload>();
            foreach (var payload in payloads)
            {
                if (!await SystemNexusPayloadVisitor.TryVisitAsync(
                        payload,
                        payload => EncodeAsync(codec, payload),
                        nestedPayloads => EncodeAsync(codec, nestedPayloads)).ConfigureAwait(false))
                {
                    codecPayloads.Add(payload);
                    continue;
                }

                if (codecPayloads.Count > 0)
                {
                    newPayloads.AddRange(await codec.EncodeAsync(codecPayloads).ConfigureAwait(false));
                    codecPayloads.Clear();
                }
                newPayloads.Add(payload);
            }
            if (codecPayloads.Count > 0)
            {
                newPayloads.AddRange(await codec.EncodeAsync(codecPayloads).ConfigureAwait(false));
            }
            payloads.Clear();
            payloads.AddRange(newPayloads);
        }

        internal static async Task EncodeAsync(IPayloadCodec codec, Payload payload)
        {
            if (await SystemNexusPayloadVisitor.TryVisitAsync(
                    payload,
                    nestedPayload => EncodeAsync(codec, nestedPayload),
                    nestedPayloads => EncodeAsync(codec, nestedPayloads)).ConfigureAwait(false))
            {
                return;
            }
            // We are gonna require a single result here. It is important that we do Single() call
            // before clearing out payload to merge with since underlying enumerable may be lazy.
            // If the returned payload is literally the same object as the one sent to the codec,
            // we leave it alone.
            var encodedList = await codec.EncodeAsync(new Payload[] { payload }).ConfigureAwait(false);
            var encoded = encodedList.Single();
            if (!ReferenceEquals(encoded, payload))
            {
                payload.Metadata.Clear();
                payload.Data = ByteString.Empty;
                payload.MergeFrom(encoded);
            }
        }

        internal static async Task DecodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads)
        {
            if (payloads.Count == 0)
            {
                return;
            }
            var newPayloads = new List<Payload>();
            var codecPayloads = new List<Payload>();
            foreach (var payload in payloads)
            {
                if (!await SystemNexusPayloadVisitor.TryVisitAsync(
                        payload,
                        payload => DecodeAsync(codec, payload),
                        nestedPayloads => DecodeAsync(codec, nestedPayloads)).ConfigureAwait(false))
                {
                    codecPayloads.Add(payload);
                    continue;
                }

                if (codecPayloads.Count > 0)
                {
                    newPayloads.AddRange(await codec.DecodeAsync(codecPayloads).ConfigureAwait(false));
                    codecPayloads.Clear();
                }
                newPayloads.Add(payload);
            }
            if (codecPayloads.Count > 0)
            {
                newPayloads.AddRange(await codec.DecodeAsync(codecPayloads).ConfigureAwait(false));
            }
            payloads.Clear();
            payloads.AddRange(newPayloads);
        }

        internal static async Task DecodeAsync(IPayloadCodec codec, Payload payload)
        {
            if (await SystemNexusPayloadVisitor.TryVisitAsync(
                    payload,
                    nestedPayload => DecodeAsync(codec, nestedPayload),
                    nestedPayloads => DecodeAsync(codec, nestedPayloads)).ConfigureAwait(false))
            {
                return;
            }
            // We are gonna require a single result here.
            // Similarly with encode, we leave the payload alone if it's exactly the same object as the original.
            var decoded = await codec.DecodeAsync(new Payload[] { payload }).ConfigureAwait(false);
            var decodedPayload = decoded.Single();
            if (!ReferenceEquals(decodedPayload, payload))
            {
                payload.Metadata.Clear();
                payload.Data = ByteString.Empty;
                payload.MergeFrom(decodedPayload);
            }
        }
    }
}
