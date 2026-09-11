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
        internal const string TemporalSystemEndpoint = "__temporal_system";

        private const string SystemPayloadMetadataKey = "__temporal_system_payload";

        private static readonly ByteString SystemPayloadMetadataValue = ByteString.CopyFromUtf8("true");

        internal delegate Task PayloadVisitor(Payload payload);

        internal delegate Task PayloadsVisitor(RepeatedField<Payload> payloads);

        internal static bool IsSystemEndpoint(string? endpoint) => endpoint == TemporalSystemEndpoint;

        internal static void MarkSystemPayload(Payload payload) =>
            payload.Metadata[SystemPayloadMetadataKey] = SystemPayloadMetadataValue;

        internal static bool IsSystemPayload(Payload payload) =>
            payload.Metadata.TryGetValue(SystemPayloadMetadataKey, out var value) &&
            value.Equals(SystemPayloadMetadataValue);

        internal static bool TryGetVisitor(Payload payload, out string messageType, out Func<Payload, PayloadVisitor, PayloadsVisitor, Task>? visit)
        {
            if (!payload.Metadata.TryGetValue("messageType", out var messageByteString))
            {
                visit = null;
                messageType = "<missing>";
                return false;
            }

            messageType = messageByteString.ToStringUtf8();

            if (!EnvelopeVisitors.TryGetValue(messageType, out visit))
            {
                visit = null;
                return false;
            }

            return true;
        }

        internal static async Task<bool> TryVisitAsync(
            Payload payload,
            PayloadVisitor visitPayload,
            PayloadsVisitor visitPayloads)
        {
            if (!IsSystemPayload(payload))
            {
                return false;
            }

            if (!TryGetVisitor(payload, out var messageType, out var visit))
            {
                throw new InvalidOperationException(
                    $"Unrecognized marked System Nexus envelope message type: {messageType}");
            }

            await visit!(payload, visitPayload, visitPayloads).ConfigureAwait(false);
            return true;
        }

        private static async Task VisitEnvelopeAsync<T>(
            Payload payload,
            Func<T, PayloadVisitor, PayloadsVisitor, Task> visitMessage,
            PayloadVisitor visitPayload,
            PayloadsVisitor visitPayloads)
            where T : IMessage<T>, new()
        {
            BinaryProtoConverter.AssertProtoPayload(payload, typeof(T));
            var message = new T();
            message.MergeFrom(payload.Data);
            await visitMessage(message, visitPayload, visitPayloads).ConfigureAwait(false);
            payload.Metadata.Clear();
            payload.Metadata["encoding"] = ByteString.CopyFromUtf8("binary/protobuf");
            payload.Metadata["messageType"] = ByteString.CopyFromUtf8(message.Descriptor.FullName);
            MarkSystemPayload(payload);
            payload.Data = message.ToByteString();
        }
    }
}
