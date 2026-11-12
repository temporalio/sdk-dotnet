using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Xunit;

namespace Temporalio.Tests
{
    /// <summary>
    /// Records every serialization context it is handed and tags each payload it encodes with the
    /// Nexus context used, refusing to decode a payload under a different context than encoded it.
    /// A test therefore passes only if the SDK decodes each payload with the exact Nexus context it
    /// was encoded with, the way a codec keyed on the context (e.g. a per-endpoint encryption key)
    /// would need.
    /// </summary>
    public class SigningRecordingCodec : RecordingPayloadCodec
    {
        public const string SignatureKey = "ser-ctx-signature";

        public SigningRecordingCodec()
            : this(new List<ISerializationContext?>(), null)
        {
        }

        private SigningRecordingCodec(List<ISerializationContext?> seen, ISerializationContext? context)
            : base(seen, context)
        {
        }

        public static string Signature(ISerializationContext.Nexus nexus) =>
            $"{nexus.Endpoint}:{nexus.Service}:{nexus.Operation}";

        /// <summary>
        /// Gets the signature an encoded payload carries, or null if it was encoded without a
        /// Nexus context. Only meaningful on raw payloads read from the server, since decoding
        /// strips the signature.
        /// </summary>
        /// <param name="payload">Encoded payload.</param>
        /// <returns>Signature, or null.</returns>
        public static string? SignatureOf(Payload? payload) =>
            payload != null && payload.Metadata.TryGetValue(SignatureKey, out var signature) ?
                signature.ToStringUtf8() : null;

        public override IPayloadCodec WithSerializationContext(ISerializationContext context) =>
            new SigningRecordingCodec(Seen, context);

        public override Task<IReadOnlyCollection<Payload>> EncodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            Record();
            if (Context is not ISerializationContext.Nexus nexus)
            {
                return Task.FromResult(payloads);
            }
            var signature = Signature(nexus);
            return Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                var copy = p.Clone();
                copy.Metadata[SignatureKey] = ByteString.CopyFromUtf8(signature);
                return copy;
            }).ToList());
        }

        public override Task<IReadOnlyCollection<Payload>> DecodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            Record();
            return Task.FromResult<IReadOnlyCollection<Payload>>(payloads.Select(p =>
            {
                if (!p.Metadata.TryGetValue(SignatureKey, out var signature))
                {
                    // Decoding under a Nexus context something that was encoded without one means
                    // the SDK picked different contexts for the two halves of a round trip.
                    Assert.False(
                        Context is ISerializationContext.Nexus,
                        $"payload encoded without a context was decoded under {Context}");
                    return p;
                }
                var nexus = Assert.IsType<ISerializationContext.Nexus>(Context);
                Assert.Equal(Signature(nexus), signature.ToStringUtf8());
                var copy = p.Clone();
                copy.Metadata.Remove(SignatureKey);
                return copy;
            }).ToList());
        }
    }
}
