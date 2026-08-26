using System;
using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Extensions.WorkflowStreams.Internal
{
    /// <summary>
    /// Encodes and decodes the base64-of-proto per-item wire format shared across the other SDKs'
    /// workflow streams packages. Internal to the workflow streams module.
    /// </summary>
    internal static class PayloadWire
    {
        /// <summary>
        /// Encodes a payload to the base64-of-proto wire format.
        /// </summary>
        /// <param name="payload">Payload to encode.</param>
        /// <returns>Base64 of the serialized payload proto bytes.</returns>
        public static string Encode(Payload payload) =>
            Convert.ToBase64String(payload.ToByteArray());

        /// <summary>
        /// Decodes the base64-of-proto wire format back to a payload.
        /// </summary>
        /// <param name="wire">Base64 of the serialized payload proto bytes.</param>
        /// <returns>The decoded payload.</returns>
        /// <exception cref="ArgumentException">
        /// The input is not valid base64 or not a valid payload.
        /// </exception>
        public static Payload Decode(string wire)
        {
            try
            {
                return Payload.Parser.ParseFrom(Convert.FromBase64String(wire));
            }
            catch (Exception e) when (e is FormatException || e is InvalidProtocolBufferException)
            {
                throw new ArgumentException("workflowstreams: unmarshal payload", e);
            }
        }

        /// <summary>
        /// Estimates the contribution of a single encoded item to a poll response.
        /// <paramref name="encoded" /> is already base64 (its on-wire representation), so this is
        /// just the character counts, matching the other SDKs.
        /// </summary>
        /// <param name="encoded">Base64-encoded item data.</param>
        /// <param name="topic">Topic the item was published on.</param>
        /// <returns>Estimated wire size in characters.</returns>
        public static int WireSize(string encoded, string topic) => encoded.Length + topic.Length;

        /// <summary>
        /// Gets whether one item is too large to fit in a paged poll response.
        /// </summary>
        /// <param name="encoded">Base64-encoded item data.</param>
        /// <param name="topic">Topic the item was published on.</param>
        /// <returns>True if the item exceeds the response cap.</returns>
        public static bool IsTooLarge(string encoded, string topic) =>
            WireSize(encoded, topic) > WorkflowStreamConstants.MaxPollResponseBytes;
    }
}
