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
            PayloadVisitors.VisitAsync(payloads, Options(codec.EncodeAsync));

        internal static Task EncodeAsync(IPayloadCodec codec, Payload payload) =>
            PayloadVisitors.VisitAsync(payload, Options(codec.EncodeAsync));

        internal static Task DecodeAsync(IPayloadCodec codec, RepeatedField<Payload> payloads) =>
            PayloadVisitors.VisitAsync(payloads, Options(codec.DecodeAsync));

        internal static Task DecodeAsync(IPayloadCodec codec, Payload payload) =>
            PayloadVisitors.VisitAsync(payload, Options(codec.DecodeAsync));

        // A codec runs one payload at a time (the default limit): it may be stateful and, in a
        // workflow, runs in the single-threaded workflow context.
        private static PayloadVisitors.VisitOptions Options(
            Func<IReadOnlyCollection<Payload>, Task<IReadOnlyCollection<Payload>>> codecCall) =>
            new(
                async (payload, _) => (await codecCall(new[] { payload }).ConfigureAwait(false)).Single(),
                (payloads, _) => codecCall(payloads));
    }
}
