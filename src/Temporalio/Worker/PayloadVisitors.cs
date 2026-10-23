#pragma warning disable SA1600 // Internal implementation plumbing.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Worker
{
    internal static partial class PayloadVisitors
    {
        internal delegate Task PayloadVisitor(Payload payload);

        internal delegate Task<IReadOnlyCollection<Payload>> PayloadsVisitor(
            IReadOnlyCollection<Payload> payloads);

        // Descends into a System Nexus envelope first, so visitPayload only ever sees payloads
        // that carry user data.
        internal static async Task VisitAsync(
            Payload payload, PayloadVisitor visitPayload, PayloadsVisitor visitPayloads)
        {
            if (!await TryVisitEnvelopeAsync(payload, visitPayload, visitPayloads).ConfigureAwait(false))
            {
                await visitPayload(payload).ConfigureAwait(false);
            }
        }

        // Hands visitPayloads the longest runs it can. Envelopes are descended into individually
        // and split the runs around them, which keeps the collection in its original order even
        // though a visitor may return a different number of payloads than it was given.
        internal static async Task VisitAsync(
            RepeatedField<Payload> payloads,
            PayloadVisitor visitPayload,
            PayloadsVisitor visitPayloads)
        {
            if (payloads.Count == 0)
            {
                return;
            }

            var visited = new List<Payload>();
            var run = new List<Payload>();
            foreach (var payload in payloads)
            {
                if (await TryVisitEnvelopeAsync(payload, visitPayload, visitPayloads).ConfigureAwait(false))
                {
                    if (run.Count > 0)
                    {
                        visited.AddRange(await visitPayloads(run).ConfigureAwait(false));
                        run.Clear();
                    }

                    visited.Add(payload);
                }
                else
                {
                    run.Add(payload);
                }
            }

            if (run.Count > 0)
            {
                visited.AddRange(await visitPayloads(run).ConfigureAwait(false));
            }

            payloads.Clear();
            payloads.AddRange(visited);
        }

        internal static bool TryGetEnvelopeVisitor(
            Payload payload,
            out string messageType,
            out Func<Payload, PayloadVisitor, PayloadsVisitor, Task>? visit)
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

        private static async Task<bool> TryVisitEnvelopeAsync(
            Payload payload, PayloadVisitor visitPayload, PayloadsVisitor visitPayloads)
        {
            if (!SystemNexusPayloads.IsSystemPayload(payload))
            {
                return false;
            }

            if (!TryGetEnvelopeVisitor(payload, out var messageType, out var visit))
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
            SystemNexusPayloads.MarkSystemPayload(payload);
            payload.Data = message.ToByteString();
        }
    }
}
