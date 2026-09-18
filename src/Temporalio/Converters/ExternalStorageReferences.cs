using System.Collections.Generic;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Sdk.V1;

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
        /// Whether the given payload is a reference to externally stored data.
        /// </summary>
        /// <param name="payload">Payload to check.</param>
        /// <returns>True if the payload is a reference.</returns>
        internal static bool IsReference(Payload payload) =>
            payload.Metadata.TryGetValue(EncodingMetadataKey, out var encoding) &&
            ProtoJsonEncoding.Equals(encoding) &&
            payload.Metadata.TryGetValue(MessageTypeMetadataKey, out var messageType) &&
            ReferenceMessageTypeBytes.Equals(messageType);

        /// <summary>
        /// Parse the reference from the given payload if it is one.
        /// </summary>
        /// <param name="payload">Payload to parse.</param>
        /// <param name="reference">The parsed reference, or null if the payload is not one.</param>
        /// <returns>True if the payload is a reference and was parsed.</returns>
        /// <exception cref="InvalidJsonException">
        /// If the payload is a reference but its data is not valid JSON.
        /// </exception>
        /// <exception cref="InvalidProtocolBufferException">
        /// If the payload is a reference but its data is not a valid reference.
        /// </exception>
        /// <remarks>
        /// Unparseable reference data is corrupt rather than an ordinary payload, so it is surfaced
        /// instead of being silently passed through as if it were user data.
        /// </remarks>
        internal static bool TryParseReference(
            Payload payload, out ExternalStorageReference reference)
        {
            reference = null!;
            if (!IsReference(payload))
            {
                return false;
            }
            reference = ProtoJsonParser.Parse<ExternalStorageReference>(payload.Data.ToStringUtf8());
            return true;
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
