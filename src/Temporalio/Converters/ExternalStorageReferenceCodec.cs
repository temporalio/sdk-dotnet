using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Temporalio.Converters
{
    /// <summary>
    /// Applies the payload codec to the references that replace stored payloads, so a reference's
    /// claim data is protected the same way an inline payload is.
    /// </summary>
    /// <remarks>
    /// An encoded reference can't be recognized by its encoding, so it carries a marker that the
    /// codec leaves alone. A codec runs one payload at a time, as it does for inline payloads. A
    /// null codec means none is configured, so a marked reference is already plain.
    /// </remarks>
    internal static class ExternalStorageReferenceCodec
    {
        /// <summary>
        /// Replace every marked reference in a message with the plain reference it encodes.
        /// </summary>
        /// <typeparam name="T">The message type being visited.</typeparam>
        /// <param name="walk">The generated <c>PayloadVisitors.Walk</c> overload.</param>
        /// <param name="message">The message to decode in place.</param>
        /// <param name="codec">Codec to decode with, or null when none is configured.</param>
        /// <param name="deriveCodec">
        /// Given each message walked and its enclosing codec, returns the codec for that message's
        /// payloads. Passing no callback keeps <paramref name="codec"/> throughout.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether the message holds any reference to retrieve.</returns>
        internal static async Task<bool> DecodeAsync<T>(
            Action<T, PayloadVisitors.WalkEnv, PayloadVisitors.VisitState> walk,
            T message,
            IPayloadCodec? codec,
            Func<IMessage, IPayloadCodec?, IPayloadCodec?>? deriveCodec,
            CancellationToken cancellationToken)
        {
            var found = false;
            await PayloadVisitors.VisitAsync(
                message,
                walk,
                new PayloadVisitors.VisitOptions<IPayloadCodec?>(
                    async (payload, codec, _) =>
                    {
                        var (decoded, isReference) = await DecodeOneAsync(payload, codec)
                            .ConfigureAwait(false);
                        found |= isReference;
                        return decoded;
                    },
                    async (payloads, codec, _) =>
                    {
                        var (decoded, anyReference) = await DecodeAsync(payloads, codec)
                            .ConfigureAwait(false);
                        found |= anyReference;
                        return decoded;
                    })
                {
                    InitialContext = codec,
                    DeriveContext = deriveCodec,
                    CancellationToken = cancellationToken,
                }).ConfigureAwait(false);
            return found;
        }

        /// <summary>
        /// Replace every marked reference in a field with the plain reference it encodes.
        /// </summary>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="codec">Codec to decode with, or null when none is configured.</param>
        /// <returns>The decoded payloads, and whether any is a reference to retrieve.</returns>
        internal static async Task<(IReadOnlyCollection<Payload> Payloads, bool AnyReference)> DecodeAsync(
            IReadOnlyCollection<Payload> payloads, IPayloadCodec? codec)
        {
            if (!payloads.Any(ExternalStorageReferences.NeedsRetrieval))
            {
                return (payloads, false);
            }
            var result = new List<Payload>(payloads.Count);
            foreach (var payload in payloads)
            {
                result.Add((await DecodeOneAsync(payload, codec).ConfigureAwait(false)).Payload);
            }
            return (result, true);
        }

        /// <summary>
        /// Encode and mark every plain reference in a message.
        /// </summary>
        /// <typeparam name="T">The message type being visited.</typeparam>
        /// <param name="walk">The generated <c>PayloadVisitors.Walk</c> overload.</param>
        /// <param name="message">The message to encode in place.</param>
        /// <param name="codec">Codec to encode with, or null when none is configured.</param>
        /// <param name="deriveCodec">
        /// Given each message walked and its enclosing codec, returns the codec for that message's
        /// payloads. Passing no callback keeps <paramref name="codec"/> throughout.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the message has been encoded.</returns>
        internal static Task EncodeAsync<T>(
            Action<T, PayloadVisitors.WalkEnv, PayloadVisitors.VisitState> walk,
            T message,
            IPayloadCodec? codec,
            Func<IMessage, IPayloadCodec?, IPayloadCodec?>? deriveCodec,
            CancellationToken cancellationToken) =>
            PayloadVisitors.VisitAsync(
                message,
                walk,
                new PayloadVisitors.VisitOptions<IPayloadCodec?>(
                    (payload, codec, _) => EncodeOneAsync(payload, codec),
                    (payloads, codec, _) => EncodeAsync(payloads, codec))
                {
                    InitialContext = codec,
                    DeriveContext = deriveCodec,
                    CancellationToken = cancellationToken,
                });

        /// <summary>
        /// Encode and mark every plain reference in a field.
        /// </summary>
        /// <param name="payloads">Payloads of one field.</param>
        /// <param name="codec">Codec to encode with, or null when none is configured.</param>
        /// <returns>The payloads with each plain reference encoded and marked.</returns>
        internal static async Task<IReadOnlyCollection<Payload>> EncodeAsync(
            IReadOnlyCollection<Payload> payloads, IPayloadCodec? codec)
        {
            if (!payloads.Any(NeedsEncoding))
            {
                return payloads;
            }
            var result = new List<Payload>(payloads.Count);
            foreach (var payload in payloads)
            {
                result.Add(await EncodeOneAsync(payload, codec).ConfigureAwait(false));
            }
            return result;
        }

        private static bool NeedsEncoding(Payload payload) =>
            ExternalStorageReferences.IsPlainReference(payload) &&
            !ExternalStorageReferences.IsMarked(payload);

        private static async Task<(Payload Payload, bool IsReference)> DecodeOneAsync(
            Payload payload, IPayloadCodec? codec)
        {
            if (!ExternalStorageReferences.IsMarked(payload))
            {
                return (payload, ExternalStorageReferences.IsPlainReference(payload));
            }
            // Decoded as it appears in history, marker included, since that is also what a codec
            // server receives.
            var decoded = codec == null ? new[] { payload } :
                await codec.DecodeAsync(new[] { payload }).ConfigureAwait(false);
            if (decoded.Count != 1 || !ExternalStorageReferences.IsPlainReference(decoded.First()))
            {
                throw new UndecodableExternalStorageReferenceException();
            }
            var reference = decoded.First();
            CopyExternalPayloadDetails(from: payload, to: reference);
            return (reference, true);
        }

        private static async Task<Payload> EncodeOneAsync(Payload payload, IPayloadCodec? codec)
        {
            if (!NeedsEncoding(payload))
            {
                return payload;
            }
            var encoded = codec == null ? new[] { payload } :
                await codec.EncodeAsync(new[] { payload }).ConfigureAwait(false);
            if (encoded.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Payload codec returned {encoded.Count} payloads for one external storage " +
                    "reference.");
            }
            var result = encoded.First();
            ExternalStorageReferences.Mark(result);
            CopyExternalPayloadDetails(from: payload, to: result);
            return result;
        }

        // The server reads offloaded sizes from the outermost payload, and a codec that builds a
        // new payload drops them.
        private static void CopyExternalPayloadDetails(Payload from, Payload to)
        {
            if (to.ExternalPayloads.Count == 0)
            {
                to.ExternalPayloads.Add(from.ExternalPayloads.Select(details => details.Clone()));
            }
        }
    }
}
