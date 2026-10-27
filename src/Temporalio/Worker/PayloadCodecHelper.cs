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
        internal static Task EncodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads) =>
            PayloadVisitors.VisitAsync(
                payloads, p => ApplyAsync(codec.EncodeAsync, p), codec.EncodeAsync);

        internal static Task EncodeAsync(IPayloadCodec codec, Payload payload) =>
            PayloadVisitors.VisitAsync(
                payload, p => ApplyAsync(codec.EncodeAsync, p), codec.EncodeAsync);

        internal static Task DecodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads) =>
            PayloadVisitors.VisitAsync(
                payloads, p => ApplyAsync(codec.DecodeAsync, p), codec.DecodeAsync);

        internal static Task DecodeAsync(IPayloadCodec codec, Payload payload) =>
            PayloadVisitors.VisitAsync(
                payload, p => ApplyAsync(codec.DecodeAsync, p), codec.DecodeAsync);

        private static async Task ApplyAsync(
            System.Func<IReadOnlyCollection<Payload>, Task<IReadOnlyCollection<Payload>>> codecCall,
            Payload payload)
        {
            // Single() runs before the payload is cleared because the returned enumerable may be
            // lazy. A codec that declined to alter the payload returns the same instance, which is
            // left alone so its identity survives for callers holding a reference.
            var result = (await codecCall(new Payload[] { payload }).ConfigureAwait(false)).Single();
            if (!ReferenceEquals(result, payload))
            {
                payload.Metadata.Clear();
                payload.Data = ByteString.Empty;
                payload.MergeFrom(result);
            }
        }
    }
}
