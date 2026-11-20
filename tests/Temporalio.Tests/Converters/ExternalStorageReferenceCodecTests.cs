namespace Temporalio.Tests.Converters;

using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Xunit;

public class ExternalStorageReferenceCodecTests
{
    [Fact]
    public async Task EncodeThenDecode_RoundTripsReferencesAndLeavesOtherPayloadsAlone()
    {
        var codec = new Base64PayloadCodec();
        var reference = Reference("a");
        var ordinary = Ordinary("plain");
        var message = new Payloads { Payloads_ = { reference.Clone(), ordinary.Clone() } };

        await ExternalStorageReferenceCodec.EncodeAsync(
            PayloadVisitors.Walk, message, codec, deriveCodec: null, default);

        var encoded = message.Payloads_[0];
        Assert.True(ExternalStorageReferences.IsMarked(encoded));
        Assert.False(ExternalStorageReferences.IsPlainReference(encoded));
        // The codec drops them, so they are restored onto the outermost payload.
        Assert.Equal(reference.ExternalPayloads, encoded.ExternalPayloads);
        Assert.Equal(ordinary, message.Payloads_[1]);

        var found = await ExternalStorageReferenceCodec.DecodeAsync(
            PayloadVisitors.Walk, message, codec, deriveCodec: null, default);

        Assert.True(found);
        Assert.Equal(reference, message.Payloads_[0]);
        Assert.Equal(ordinary, message.Payloads_[1]);
    }

    [Fact]
    public async Task Decode_ReportsWhetherAnyReferenceNeedsRetrieving()
    {
        var codec = new Base64PayloadCodec();

        var none = new Payloads { Payloads_ = { Ordinary("plain") } };
        Assert.False(await ExternalStorageReferenceCodec.DecodeAsync(
            PayloadVisitors.Walk, none, codec, deriveCodec: null, default));

        var plain = new Payloads { Payloads_ = { Reference("a") } };
        Assert.True(await ExternalStorageReferenceCodec.DecodeAsync(
            PayloadVisitors.Walk, plain, codec, deriveCodec: null, default));
    }

    [Fact]
    public async Task WithoutCodec_ReferencesAreMarkedAndStillPlain()
    {
        var reference = Reference("a");

        var encoded = Assert.Single(
            await ExternalStorageReferenceCodec.EncodeAsync(new[] { reference.Clone() }, codec: null));
        Assert.True(ExternalStorageReferences.IsMarked(encoded));
        Assert.True(ExternalStorageReferences.IsPlainReference(encoded));

        var (decoded, anyReference) =
            await ExternalStorageReferenceCodec.DecodeAsync(new[] { encoded }, codec: null);
        Assert.True(anyReference);
        Assert.True(ExternalStorageReferences.IsPlainReference(Assert.Single(decoded)));
    }

    [Fact]
    public async Task Encode_LeavesAMarkedReferenceAlone()
    {
        var marked = Reference("a");
        ExternalStorageReferences.Mark(marked);

        var result = await ExternalStorageReferenceCodec.EncodeAsync(
            new[] { marked }, new Base64PayloadCodec());

        Assert.Same(marked, Assert.Single(result));
    }

    [Fact]
    public async Task Decode_MarkedPayloadThatIsNotAReference_Throws()
    {
        var marked = Ordinary("not a reference");
        ExternalStorageReferences.Mark(marked);

        await Assert.ThrowsAsync<UndecodableExternalStorageReferenceException>(
            () => ExternalStorageReferenceCodec.DecodeAsync(new[] { marked }, codec: null));
    }

    [Fact]
    public async Task Decode_WithTheWrongCodec_Throws()
    {
        var encoded = Assert.Single(await ExternalStorageReferenceCodec.EncodeAsync(
            new[] { Reference("a") }, new Base64PayloadCodec()));

        // Without the codec that wrote it, the marked payload is not a plain reference.
        await Assert.ThrowsAsync<UndecodableExternalStorageReferenceException>(
            () => ExternalStorageReferenceCodec.DecodeAsync(new[] { encoded }, codec: null));
    }

    [Fact]
    public async Task Encode_CodecReturningOtherThanOnePayload_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExternalStorageReferenceCodec.EncodeAsync(
                new[] { Reference("a") }, new MiscountingCodec(2)));
    }

    private static Payload Reference(string key) =>
        ExternalStorageReferences.CreateReferencePayload(
            "mem", new Dictionary<string, string> { ["key"] = key }, originalSizeBytes: 1234);

    private static Payload Ordinary(string data) =>
        new()
        {
            Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") },
            Data = ByteString.CopyFromUtf8(data),
        };

    // Returns the given number of empty payloads, whatever it is given.
    private sealed class MiscountingCodec : IPayloadCodec
    {
        private readonly int count;

        public MiscountingCodec(int count) => this.count = count;

        public Task<IReadOnlyCollection<Payload>> EncodeAsync(IReadOnlyCollection<Payload> payloads) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(
                Enumerable.Range(0, count).Select(_ => new Payload()).ToList());

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(IReadOnlyCollection<Payload> payloads) =>
            EncodeAsync(payloads);
    }
}
