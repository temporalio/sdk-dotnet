#pragma warning disable SA1600 // Internal implementation plumbing.

using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    internal static partial class SystemNexusPayloadVisitor
    {
        private static readonly BinaryProtoConverter ProtoPayloadConverter = new();

        internal delegate Task PayloadVisitor(Payload payload);

        internal delegate Task PayloadsVisitor(RepeatedField<Payload> payloads);

        internal delegate Task EnvelopeVisitor(Payload payload);

        internal static bool TryToInputPayload(
            string? endpoint,
            object? value,
            out Payload payload)
        {
            payload = null!;
            if (!IsSystemNexusEndpoint(endpoint))
            {
                return false;
            }

            if (!ProtoPayloadConverter.TryToPayload(value, out var converted) || converted == null)
            {
                return false;
            }

            payload = converted;
            return true;
        }

        internal static Task<bool> TryVisitInputAsync(
            string? endpoint,
            Payload payload,
            PayloadVisitor visitPayload,
            PayloadsVisitor visitPayloads,
            EnvelopeVisitor? visitEnvelope = null) =>
            TryVisitAsync(endpoint, payload, visitPayload, visitPayloads, visitEnvelope);

        internal static Task<bool> TryVisitOutputAsync(
            string? endpoint,
            Payload payload,
            PayloadVisitor visitPayload,
            PayloadsVisitor visitPayloads,
            EnvelopeVisitor? visitEnvelope = null) =>
            TryVisitAsync(endpoint, payload, visitPayload, visitPayloads, visitEnvelope);

        private static async Task VisitEnvelopeAsync<T>(
            Payload payload,
            Func<T, PayloadVisitor, PayloadsVisitor, Task> visitMessage,
            PayloadVisitor visitPayload,
            PayloadsVisitor visitPayloads,
            EnvelopeVisitor? visitEnvelope)
            where T : IMessage<T>, new()
        {
            BinaryProtoConverter.AssertProtoPayload(payload, typeof(T));
            var message = new T();
            message.MergeFrom(payload.Data);
            await visitMessage(message, visitPayload, visitPayloads).ConfigureAwait(false);
            payload.Metadata.Clear();
            payload.Metadata["encoding"] = ByteString.CopyFromUtf8("binary/protobuf");
            payload.Metadata["messageType"] = ByteString.CopyFromUtf8(message.Descriptor.FullName);
            payload.Data = message.ToByteString();
            if (visitEnvelope != null)
            {
                await visitEnvelope(payload).ConfigureAwait(false);
            }
        }
    }
}
