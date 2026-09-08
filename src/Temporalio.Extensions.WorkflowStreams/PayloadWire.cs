using System;
using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Extensions.WorkflowStreams
{
    /// <summary>Preserves payload metadata in cross-language JSON envelopes.</summary>
    internal static class PayloadWire
    {
        /// <summary>Encodes a payload as base64 protobuf bytes.</summary>
        /// <param name="payload">Payload to encode.</param>
        /// <returns>The padded base64 wire value.</returns>
        internal static string Encode(Payload payload) =>
            Convert.ToBase64String(payload.ToByteArray());

        /// <summary>Decodes base64 protobuf bytes into a payload.</summary>
        /// <param name="data">Padded base64 wire value.</param>
        /// <returns>The decoded payload.</returns>
        internal static Payload Decode(string data) =>
            Payload.Parser.ParseFrom(Convert.FromBase64String(data));

        /// <summary>Approximates an item's JSON response contribution consistently with peers.</summary>
        /// <param name="encodedData">Base64 payload data.</param>
        /// <param name="topic">Item topic.</param>
        /// <returns>The approximate byte contribution.</returns>
        internal static int EstimateSize(string encodedData, string topic) =>
            encodedData.Length + topic.Length;
    }
}
