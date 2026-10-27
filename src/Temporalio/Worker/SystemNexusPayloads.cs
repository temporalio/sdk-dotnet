#pragma warning disable SA1600 // Internal implementation plumbing.

using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Worker
{
    /// <summary>
    /// Identification of the System Nexus endpoint and of the payloads that carry its envelopes.
    /// Descending into an envelope is <see cref="PayloadVisitors"/>' job, so nothing that merely
    /// applies an operation to payloads needs this.
    /// </summary>
    internal static class SystemNexusPayloads
    {
        internal const string TemporalSystemEndpoint = "__temporal_system";

        private const string SystemPayloadMetadataKey = "__temporal_system_payload";

        private static readonly ByteString SystemPayloadMetadataValue = ByteString.CopyFromUtf8("true");

        internal static bool IsSystemEndpoint(string? endpoint) => endpoint == TemporalSystemEndpoint;

        internal static void MarkSystemPayload(Payload payload) =>
            payload.Metadata[SystemPayloadMetadataKey] = SystemPayloadMetadataValue;

        internal static bool IsSystemPayload(Payload payload) =>
            payload.Metadata.TryGetValue(SystemPayloadMetadataKey, out var value) &&
            value.Equals(SystemPayloadMetadataValue);
    }
}
