namespace Temporalio.Tests.Converters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Xunit;
using Xunit.Abstractions;

public class ExternalStorageRunnerTests : TestBase
{
    public ExternalStorageRunnerTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task StoreAsync_AtTheThreshold_OffloadsAndLeavesSmallerInline()
    {
        var small = Sized(8);
        var atThreshold = Sized(64);
        var threshold = atThreshold.CalculateSize();
        Assert.True(small.CalculateSize() < threshold);
        var driver = new MapDriver("mem");
        var runner = Runner(Storage(driver, threshold));

        var result = (await runner.StoreAsync(
            new[] { small, atThreshold }, null, default)).ToList();

        Assert.Same(small, result[0]);
        Assert.True(ExternalStorageReferences.IsReference(result[1]));
        // What the driver was handed is byte-equal to what history would have held inline.
        Assert.Equal(atThreshold, driver.Stored.Single().Value);
    }

    [Fact]
    public async Task StoreAsync_Reference_RecordsThePreOffloadSize()
    {
        var payload = Sized(100);
        var size = payload.CalculateSize();
        var runner = Runner(Storage(new MapDriver("mem"), threshold: 1));

        var result = (await runner.StoreAsync(new[] { payload }, null, default)).Single();

        Assert.Equal(size, result.ExternalPayloads[0].SizeBytes);
    }

    [Fact]
    public async Task StoreAsync_SelectorDeclines_LeavesPayloadInline()
    {
        var routed = Filled((byte)'a', 50);
        var declined = Filled((byte)'z', 50);
        var driver = new MapDriver("a");
        // Routes a payload beginning with 'a', declines one beginning with 'z'.
        var storage = new ExternalStorage(
            new IStorageDriver[] { driver },
            (context, payload) => payload.Data[0] == (byte)'a' ? driver : null)
        {
            PayloadSizeThreshold = 1,
        };
        var runner = Runner(storage);

        var result = (await runner.StoreAsync(new[] { routed, declined }, null, default)).ToList();

        Assert.True(ExternalStorageReferences.IsReference(result[0]));
        Assert.Same(declined, result[1]);
    }

    [Fact]
    public async Task StoreAsync_ThresholdOfZero_OffloadsEveryPayload()
    {
        var runner = Runner(Storage(new MapDriver("mem"), threshold: 0));

        var result = (await runner.StoreAsync(
            new[] { Sized(1), Sized(8) }, null, default)).ToList();

        Assert.All(result, p => Assert.True(ExternalStorageReferences.IsReference(p)));
    }

    [Fact]
    public async Task StoreAsync_TwoDrivers_GroupsAndRestoresInOrder()
    {
        var a = new MapDriver("a");
        var b = new MapDriver("b");
        var storage = new ExternalStorage(
            new IStorageDriver[] { a, b },
            (context, payload) => payload.Data[0] == (byte)'a' ? a : b)
        {
            PayloadSizeThreshold = 1,
        };
        var runner = Runner(storage);
        var payloads = new[]
        {
            Filled((byte)'a', 50), Filled((byte)'b', 50),
            Filled((byte)'a', 50), Filled((byte)'b', 50),
        };

        var result = (await runner.StoreAsync(payloads, null, default)).ToList();

        Assert.All(result, p => Assert.True(ExternalStorageReferences.IsReference(p)));
        // Each driver was called once with its two payloads.
        Assert.Equal(new[] { 2 }, a.Batches);
        Assert.Equal(new[] { 2 }, b.Batches);
        // References landed back on the positions they came from.
        Assert.Equal("a", ExternalStorageReferences.ParseReference(result[0]).DriverName);
        Assert.Equal("b", ExternalStorageReferences.ParseReference(result[1]).DriverName);
        Assert.Equal("a", ExternalStorageReferences.ParseReference(result[2]).DriverName);
        Assert.Equal("b", ExternalStorageReferences.ParseReference(result[3]).DriverName);
    }

    [Fact]
    public async Task StoreAsync_SelectorReturnsUnregisteredDriver_Throws()
    {
        var registered = new MapDriver("mem");
        var impostor = new MapDriver("mem");
        var storage = new ExternalStorage(
            new IStorageDriver[] { registered }, (context, payload) => impostor)
        {
            PayloadSizeThreshold = 1,
        };
        var runner = Runner(storage);

        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.StoreAsync(new[] { Sized(50) }, null, default));
        Assert.Contains("not registered", err.Message);
    }

    [Fact]
    public async Task StoreAsync_DriverReturnsWrongNumberOfClaims_Throws()
    {
        var runner = Runner(Storage(new MiscountDriver("mem"), threshold: 1));

        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.StoreAsync(new[] { Sized(50) }, null, default));
        Assert.Contains("1 payloads", err.Message);
    }

    [Fact]
    public async Task RetrieveAsync_ReplacesReferencesAndLeavesInlineUntouched()
    {
        var driver = new MapDriver("mem");
        var runner = Runner(Storage(driver, threshold: 1));
        var original = Sized(50);
        var reference = (await runner.StoreAsync(new[] { original }, null, default)).Single();
        var inline = Sized(8);

        var result = (await runner.RetrieveAsync(new[] { inline, reference }, default)).ToList();

        Assert.Same(inline, result[0]);
        Assert.Equal(original, result[1]);
    }

    [Fact]
    public async Task RetrieveAsync_UnregisteredDriver_Throws()
    {
        var driver = new MapDriver("mem");
        var runner = Runner(Storage(driver, threshold: 1));
        var known = ExternalStorageReferences.CreateReferencePayload(
            "mem", new Dictionary<string, string> { ["key"] = "k0" }, 10);
        var unknown = ExternalStorageReferences.CreateReferencePayload(
            "nope", new Dictionary<string, string> { ["key"] = "k0" }, 10);

        await Assert.ThrowsAsync<InvalidExternalStorageReferenceException>(
            () => runner.RetrieveAsync(new[] { known, unknown }, default));
        // Drivers are resolved before any request, so the registered one was never called.
        Assert.Empty(driver.Batches);
    }

    [Fact]
    public async Task StoreAsync_SinglePayload_ReplacesInPlace()
    {
        var runner = Runner(Storage(new MapDriver("mem"), threshold: 1));
        var payload = Sized(50);

        await runner.StoreAsync(payload, null, default);

        Assert.True(ExternalStorageReferences.IsReference(payload));
    }

    [Fact]
    public async Task StoreAsync_SinglePayloadBelowThreshold_LeavesItUnchanged()
    {
        var runner = Runner(Storage(new MapDriver("mem"), threshold: 1_000_000));
        var payload = Sized(8);
        var before = payload.Clone();

        await runner.StoreAsync(payload, null, default);

        Assert.Equal(before, payload);
    }

    [Fact]
    public async Task StoreAsync_SinglePayload_LeavesTheDriversPayloadIntact()
    {
        var driver = new MapDriver("mem");
        var runner = Runner(Storage(driver, threshold: 1));
        var payload = Sized(50);
        var before = payload.Clone();

        await runner.StoreAsync(payload, null, default);

        Assert.Equal(before, driver.Stored.Single().Value);
    }

    [Fact]
    public async Task RetrieveAsync_SinglePayload_ReplacesInPlace()
    {
        var runner = Runner(Storage(new MapDriver("mem"), threshold: 1));
        var payload = Sized(50);
        var expected = payload.Clone();

        await runner.StoreAsync(payload, null, default);
        Assert.True(ExternalStorageReferences.IsReference(payload));
        await runner.RetrieveAsync(payload, default);

        Assert.Equal(expected, payload);
    }

    [Fact]
    public async Task RetrieveAsync_TwoDrivers_RunConcurrently()
    {
        // Both drivers share a two-party barrier: each retrieve blocks until the other is also
        // running, which only happens if they overlap. Serial execution would block the first
        // forever, so the timeout fails such a regression instead of hanging on it.
        var barrier = new AsyncBarrier(2);
        var a = new BarrierDriver("a", barrier);
        var b = new BarrierDriver("b", barrier);
        var storage = new ExternalStorage(new IStorageDriver[] { a, b }, (context, payload) => a);
        var runner = Runner(storage);
        var refA = ExternalStorageReferences.CreateReferencePayload(
            "a", new Dictionary<string, string> { ["key"] = "one" }, 1);
        var refB = ExternalStorageReferences.CreateReferencePayload(
            "b", new Dictionary<string, string> { ["key"] = "two" }, 1);

        var result = (await runner.RetrieveAsync(new[] { refA, refB }, default)
            .WaitAsync(TimeSpan.FromSeconds(5))).ToList();

        Assert.Equal("one", result[0].Data.ToStringUtf8());
        Assert.Equal("two", result[1].Data.ToStringUtf8());
    }

    [Fact]
    public async Task RetrieveAsync_DriverReturnsWrongNumberOfPayloads_Throws()
    {
        var runner = Runner(Storage(new MiscountDriver("mem"), threshold: 1));
        var reference = ExternalStorageReferences.CreateReferencePayload(
            "mem", new Dictionary<string, string> { ["key"] = "k0" }, 10);

        var err = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RetrieveAsync(new[] { reference }, default));
        Assert.Contains("1 claims", err.Message);
    }

    [Fact]
    public async Task StoreThenRetrieve_RoundTrips()
    {
        var runner = Runner(Storage(new MapDriver("mem"), threshold: 1));
        var payloads = new[] { Sized(50), Sized(80) };
        var before = payloads.Select(p => p.Clone()).ToArray();

        var stored = await runner.StoreAsync(payloads, null, default);
        Assert.All(stored, p => Assert.True(ExternalStorageReferences.IsReference(p)));
        var back = (await runner.RetrieveAsync(stored, default)).ToList();

        Assert.Equal(before[0], back[0]);
        Assert.Equal(before[1], back[1]);
    }

    [Fact]
    public void ThrowIfContainsReference_Reference_ThrowsTmprl1105()
    {
        var reference = ExternalStorageReferences.CreateReferencePayload(
            "mem", new Dictionary<string, string> { ["key"] = "k0" }, 10);

        ExternalStorageRunner.ThrowIfContainsReference(new[] { Sized(8) });
        Assert.Throws<ExternalStorageNotConfiguredException>(() =>
            ExternalStorageRunner.ThrowIfContainsReference(new[] { reference }));
    }

    private static ExternalStorageRunner Runner(ExternalStorage storage) =>
        new ExternalStorageRunner(
            storage, new PassThroughLimiter<Payload>(), new PassThroughLimiter<StorageDriverClaim>());

    private static ExternalStorage Storage(IStorageDriver driver, int threshold) =>
        new ExternalStorage(driver) { PayloadSizeThreshold = threshold };

    private static Payload Filled(byte b, int bytes) => new Payload()
    {
        Data = ByteString.CopyFrom(Enumerable.Repeat(b, bytes).ToArray()),
    };

    private static Payload Sized(int bytes) => Filled((byte)'x', bytes);

    private sealed class MapDriver : IStorageDriver
    {
        public MapDriver(string name) => Name = name;

        public string Name { get; }

        public string Type => "test.mapdriver";

        public Dictionary<string, Payload> Stored { get; } = new();

        public List<int> Batches { get; } = new();

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken)
        {
            Batches.Add(payloads.Count);
            var claims = new List<StorageDriverClaim>();
            foreach (var payload in payloads)
            {
                var key = $"k{Stored.Count}";
                // Kept as handed over rather than copied, as an in-memory or deferred-upload driver
                // would, so a runner that mutates a payload after giving it away is caught.
                Stored[key] = payload;
                claims.Add(new StorageDriverClaim(new Dictionary<string, string> { ["key"] = key }));
            }
            return Task.FromResult<IReadOnlyCollection<StorageDriverClaim>>(claims);
        }

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken)
        {
            Batches.Add(claims.Count);
            var payloads = claims.Select(c => Stored[c.ClaimData["key"]]).ToList();
            return Task.FromResult<IReadOnlyCollection<Payload>>(payloads);
        }
    }

    private sealed class AsyncBarrier
    {
        private readonly int count;
        private readonly TaskCompletionSource<bool> released = new();
        private int arrived;

        public AsyncBarrier(int count) => this.count = count;

        public Task<bool> SignalAndWaitAsync()
        {
            if (Interlocked.Increment(ref arrived) == count)
            {
                released.TrySetResult(true);
            }
            return released.Task;
        }
    }

    private sealed class BarrierDriver : IStorageDriver
    {
        private readonly AsyncBarrier barrier;

        public BarrierDriver(string name, AsyncBarrier barrier)
        {
            Name = name;
            this.barrier = barrier;
        }

        public string Name { get; }

        public string Type => "test.barrierdriver";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public async Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken)
        {
            await barrier.SignalAndWaitAsync();
            return claims
                .Select(c => new Payload() { Data = ByteString.CopyFromUtf8(c.ClaimData["key"]) })
                .ToList();
        }
    }

    private sealed class PassThroughLimiter<TItem> : IStorageDriverLimiter<TItem>
    {
        public Task<TResult> RunAsync<TResult>(
            TItem item, Func<Task<TResult>> operation, CancellationToken cancellationToken) =>
            operation();
    }

    private sealed class MiscountDriver : IStorageDriver
    {
        public MiscountDriver(string name) => Name = name;

        public string Name { get; }

        public string Type => "test.miscountdriver";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<StorageDriverClaim>>(Array.Empty<StorageDriverClaim>());

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(Array.Empty<Payload>());
    }
}
