namespace Temporalio.Tests.Converters;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Xunit;

public class ExternalStorageOperationsTests
{
    [Fact]
    public async Task RetrieveAsync_NoStorage_RejectsMarkedReferenceWithoutDecoding()
    {
        var runner = ExternalStorageRunner.Create(null, @namespace: null);
        var marked = new Payload { Data = ByteString.CopyFromUtf8("encoded") };
        ExternalStorageReferences.Mark(marked);
        var codec = new RecordingPayloadCodec();

        await Assert.ThrowsAsync<ExternalStorageNotConfiguredException>(
            () => ExternalStorageOperations.RetrieveAsync(
                runner,
                PayloadVisitors.Walk,
                new Payloads { Payloads_ = { marked } },
                codec,
                deriveCodec: null,
                metrics: null,
                CancellationToken.None));
        Assert.Empty(codec.Contexts);
    }

    [Fact]
    public async Task StoreAsync_EncodeReferences_EncodesAndMarksThenRetrieveDecodes()
    {
        var runner = Runner(new ExternalStorage(new MapDriver())
        {
            PayloadSizeThreshold = 1,
            EncodeReferences = true,
        });
        var codec = new Base64PayloadCodec();
        var original = Sized(100);

        var stored = Assert.Single(await ExternalStorageOperations.StoreAsync(
            runner, new[] { original }, null, codec, metrics: null, default));
        Assert.True(ExternalStorageReferences.IsMarked(stored));
        Assert.False(ExternalStorageReferences.IsPlainReference(stored));

        var retrieved = await ExternalStorageOperations.RetrieveAsync(
            runner, new[] { stored }, codec, metrics: null, default);
        Assert.Equal(original, Assert.Single(retrieved));
    }

    [Fact]
    public async Task StoreAsync_EncodeReferencesOff_LeavesReferencesPlain()
    {
        var runner = Runner(new ExternalStorage(new MapDriver()) { PayloadSizeThreshold = 1 });
        var message = new Payloads { Payloads_ = { Sized(100) } };

        await ExternalStorageOperations.StoreAsync(
            runner,
            PayloadVisitors.Walk,
            message,
            null,
            null,
            new Base64PayloadCodec(),
            deriveCodec: null,
            metrics: null,
            default);

        var stored = Assert.Single(message.Payloads_);
        Assert.True(ExternalStorageReferences.IsPlainReference(stored));
        Assert.False(ExternalStorageReferences.IsMarked(stored));
    }

    private static ExternalStorageRunner Runner(ExternalStorage storage) =>
        new(
            storage,
            null,
            UnboundedStorageDriverLimiter<Payload>.Instance,
            UnboundedStorageDriverLimiter<StorageDriverClaim>.Instance);

    private static Payload Sized(int bytes) =>
        new() { Data = ByteString.CopyFrom(Enumerable.Repeat((byte)'x', bytes).ToArray()) };

    private sealed class MapDriver : IStorageDriver
    {
        private readonly Dictionary<string, Payload> stored = new();

        public string Name => "mem";

        public string Type => "test.mapdriver";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken)
        {
            var claims = new List<StorageDriverClaim>();
            foreach (var payload in payloads)
            {
                var key = $"k{stored.Count}";
                stored[key] = payload;
                claims.Add(new StorageDriverClaim(new Dictionary<string, string> { ["key"] = key }));
            }
            return Task.FromResult<IReadOnlyCollection<StorageDriverClaim>>(claims);
        }

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(
                claims.Select(c => stored[c.ClaimData["key"]]).ToList());
    }
}
