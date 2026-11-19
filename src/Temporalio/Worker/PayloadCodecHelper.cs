#pragma warning disable SA1600 // Internal implementation plumbing.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    internal static class PayloadCodecHelper
    {
        internal static Task EncodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads) =>
            PayloadVisitors.VisitAsync(payloads, Options(codec, (c, p) => c.EncodeAsync(p)));

        internal static Task EncodeAsync(IPayloadCodec codec, Payload payload) =>
            PayloadVisitors.VisitAsync(payload, Options(codec, (c, p) => c.EncodeAsync(p)));

        internal static Task DecodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads) =>
            PayloadVisitors.VisitAsync(payloads, Options(codec, (c, p) => c.DecodeAsync(p)));

        internal static Task DecodeAsync(IPayloadCodec codec, Payload payload) =>
            PayloadVisitors.VisitAsync(payload, Options(codec, (c, p) => c.DecodeAsync(p)));

        // The codec is the walk's context, so a deriver can switch codecs for part of a message. A
        // codec runs one payload at a time (the default limit): it may be stateful and, in a
        // workflow, runs in the single-threaded workflow context.
        private static PayloadVisitors.VisitOptions<IPayloadCodec> Options(
            IPayloadCodec codec,
            Func<IPayloadCodec, IReadOnlyCollection<Payload>, Task<IReadOnlyCollection<Payload>>> apply) =>
            new(
                async (payload, codec, _) =>
                    (await apply(codec, new[] { payload }).ConfigureAwait(false)).Single(),
                (payloads, codec, _) => apply(codec, payloads))
            {
                InitialContext = codec,
            };
    }
}
