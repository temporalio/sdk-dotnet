using System;
using System.Collections.Generic;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Sdk.V1;
using Temporalio.Exceptions;

namespace Temporalio.Converters
{
    /// <summary>
    /// The on-the-wire form of a payload that has been offloaded to external storage.
    /// </summary>
    /// <remarks>
    /// An offloaded payload is replaced by one whose data is the proto-JSON form of
    /// <see cref="ExternalStorageReference" />. This shape is part of the wire contract and must
    /// not be changed unilaterally.
    /// </remarks>
    internal static class ExternalStorageReferences
    {
        private const string EncodingMetadataKey = "encoding";
        private const string MessageTypeMetadataKey = "messageType";
        private const string MarkerMetadataKey = "__temporal_external_storage_reference";

        private static readonly ByteString MarkerValue = ByteString.CopyFromUtf8("true");

        private static readonly ByteString ProtoJsonEncoding =
            ByteString.CopyFromUtf8("json/protobuf");

        private static readonly ByteString ReferenceMessageTypeBytes =
            ByteString.CopyFromUtf8(ExternalStorageReference.Descriptor.FullName);

        // Another SDK may add fields to the reference before this one knows about them, and an
        // unknown field must not make an otherwise-valid payload unreadable.
        private static readonly JsonParser ProtoJsonParser =
            new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

        /// <summary>
        /// Gets the <c>messageType</c> metadata value that identifies a reference payload.
        /// </summary>
        internal static string ReferenceMessageType { get; } =
            ExternalStorageReference.Descriptor.FullName;

        /// <summary>
        /// Whether the given payload has to be retrieved from external storage. Reads metadata
        /// only, so no payload is decoded and one that needs no retrieval is not touched.
        /// </summary>
        /// <param name="payload">Payload to check.</param>
        /// <returns>True if the payload has to be retrieved.</returns>
        internal static bool NeedsRetrieval(Payload payload) =>
            IsMarked(payload) || IsPlainReference(payload);

        /// <summary>
        /// Whether the given payload is a reference in the form a codec has not encoded, so its
        /// data can be parsed directly. A marked payload that a codec encoded must be decoded
        /// before it is parsed.
        /// </summary>
        /// <param name="payload">Payload to check.</param>
        /// <returns>True if the payload is a plain reference.</returns>
        /// <remarks>
        /// Both halves are required: <c>json/protobuf</c> on its own is an ordinary encoding for
        /// any protobuf payload.
        /// </remarks>
        internal static bool IsPlainReference(Payload payload) =>
            payload.Metadata.TryGetValue(EncodingMetadataKey, out var encoding) &&
            ProtoJsonEncoding.Equals(encoding) &&
            payload.Metadata.TryGetValue(MessageTypeMetadataKey, out var messageType) &&
            ReferenceMessageTypeBytes.Equals(messageType);

        /// <summary>
        /// Whether the given payload carries the reference marker. The value is checked, not just
        /// the key.
        /// </summary>
        /// <param name="payload">Payload to check.</param>
        /// <returns>True if the payload is marked.</returns>
        internal static bool IsMarked(Payload payload) =>
            payload.Metadata.TryGetValue(MarkerMetadataKey, out var marker) &&
            MarkerValue.Equals(marker);

        /// <summary>
        /// Mark a reference so a reader finds it without decoding anything.
        /// </summary>
        /// <param name="payload">Reference payload to mark, modified in place.</param>
        /// <remarks>
        /// The SDK does this after the codec has encoded the reference, so the marker lands on the
        /// outermost payload. With no codec configured the reference is itself outermost and is
        /// marked just the same.
        /// </remarks>
        internal static void Mark(Payload payload) =>
            payload.Metadata[MarkerMetadataKey] = MarkerValue;

        /// <summary>
        /// Parse the reference from the given payload, which must be one.
        /// </summary>
        /// <param name="payload">Payload to parse. Check <see cref="IsPlainReference" /> first.</param>
        /// <returns>The parsed reference.</returns>
        /// <exception cref="InvalidExternalStorageReferenceException">
        /// If the payload is not a reference, or its data cannot be read as one.
        /// </exception>
        internal static ExternalStorageReference ParseReference(Payload payload)
        {
            if (!IsPlainReference(payload))
            {
                throw new InvalidExternalStorageReferenceException(inner: null);
            }
            try
            {
                return ProtoJsonParser.Parse<ExternalStorageReference>(payload.Data.ToStringUtf8());
            }
            catch (Exception e) when (
                e is InvalidJsonException || e is InvalidProtocolBufferException)
            {
                throw new InvalidExternalStorageReferenceException(e);
            }
        }

        /// <summary>
        /// Create the reference payload that replaces an offloaded payload on the wire.
        /// </summary>
        /// <param name="driverName">Name of the driver that stored the payload.</param>
        /// <param name="claimData">Driver data identifying the stored payload.</param>
        /// <param name="originalSizeBytes">Encoded size of the payload that was offloaded.</param>
        /// <returns>The reference payload.</returns>
        internal static Payload CreateReferencePayload(
            string driverName, IReadOnlyDictionary<string, string> claimData, long originalSizeBytes)
        {
            var reference = new ExternalStorageReference() { DriverName = driverName };
            foreach (var claim in claimData)
            {
                reference.ClaimData[claim.Key] = claim.Value;
            }
            var payload = new Payload()
            {
                Data = ByteString.CopyFromUtf8(JsonFormatter.Default.Format(reference)),
            };
            payload.Metadata[EncodingMetadataKey] = ProtoJsonEncoding;
            payload.Metadata[MessageTypeMetadataKey] = ReferenceMessageTypeBytes;
            // The original size is kept so the server and UI can report what the payload would have
            // been without having to fetch it from storage.
            payload.ExternalPayloads.Add(
                new Payload.Types.ExternalPayloadDetails() { SizeBytes = originalSizeBytes });
            return payload;
        }
    }
}
