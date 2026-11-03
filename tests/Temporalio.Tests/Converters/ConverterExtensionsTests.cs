namespace Temporalio.Tests.Converters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Failure.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Xunit;
using Xunit.Abstractions;

public class ConverterExtensionsTests : TestBase
{
    public ConverterExtensionsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public async Task ToSingleValueAsync_ReferenceWithoutStorage_ThrowsTmprl1105()
    {
        var reference = ExternalStorageReferences.CreateReferencePayload(
            "mem", new Dictionary<string, string> { ["key"] = "k" }, originalSizeBytes: 1024);

        var error = await Assert.ThrowsAsync<ExternalStorageNotConfiguredException>(
            () => DataConverter.Default.ToValueAsync<object>(reference));
        Assert.Contains("[TMPRL1105]", error.Message);
    }

    [Fact]
    public async Task ToSingleValueAsync_ReferenceWithStorage_RetrievesBeforeDecode()
    {
        var converter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(
                new FixedDriver("mem", DataConverter.Default.PayloadConverter.ToPayload("retrieved"))),
        };
        var reference = ExternalStorageReferences.CreateReferencePayload(
            "mem", new Dictionary<string, string> { ["key"] = "k" }, originalSizeBytes: 1024);

        Assert.Equal("retrieved", await converter.ToValueAsync<string>(reference));
    }

    [Fact]
    public async Task ToExceptionAsync_ReferenceWithoutStorage_ThrowsTmprl1105()
    {
        var error = await Assert.ThrowsAsync<ExternalStorageNotConfiguredException>(
            () => DataConverter.Default.ToExceptionAsync(FailureWithReferenceDetail()));
        Assert.Contains("[TMPRL1105]", error.Message);
    }

    [Fact]
    public async Task ToExceptionAsync_ReferenceWithStorage_RetrievesBeforeConvert()
    {
        var converter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(
                new FixedDriver("mem", DataConverter.Default.PayloadConverter.ToPayload("retrieved"))),
        };

        var exception = await converter.ToExceptionAsync(FailureWithReferenceDetail());

        var application = Assert.IsType<ApplicationFailureException>(exception);
        Assert.Equal("retrieved", application.Details.ElementAt<string>(0));
    }

    [Fact]
    public async Task ToPayloadsAsync_OverThreshold_OffloadsToReference()
    {
        var converter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(new MapDriver("mem")) { PayloadSizeThreshold = 1 },
        };

        var payloads = await converter.ToPayloadsAsync(new object?[] { "hello" });

        Assert.True(ExternalStorageReferences.IsPlainReference(Assert.Single(payloads)));
    }

    [Fact]
    public async Task ToPayloadAsync_OverThreshold_OffloadsToReference()
    {
        var converter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(new MapDriver("mem")) { PayloadSizeThreshold = 1 },
        };

        Assert.True(ExternalStorageReferences.IsPlainReference(
            await converter.ToPayloadAsync("hello")));
    }

    [Fact]
    public async Task ToPayloadsAsync_ThenToSingleValueAsync_RoundTripsThroughStorage()
    {
        var converter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(new MapDriver("mem")) { PayloadSizeThreshold = 1 },
        };

        var payloads = await converter.ToPayloadsAsync(new object?[] { "hello" });
        Assert.True(ExternalStorageReferences.IsPlainReference(Assert.Single(payloads)));

        Assert.Equal("hello", await converter.ToSingleValueAsync<string>(payloads));
    }

    [Fact]
    public async Task ToFailureAsync_ThenToExceptionAsync_RoundTripsDetailThroughStorage()
    {
        var converter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(new MapDriver("mem")) { PayloadSizeThreshold = 1 },
        };
        var exception = new ApplicationFailureException(
            "boom", errorType: "MyError", details: new object?[] { "detail" });

        var failure = await converter.ToFailureAsync(exception);
        Assert.True(ExternalStorageReferences.IsPlainReference(
            failure.ApplicationFailureInfo.Details.Payloads_.Single()));

        var roundTripped = Assert.IsType<ApplicationFailureException>(
            await converter.ToExceptionAsync(failure));
        Assert.Equal("detail", roundTripped.Details.ElementAt<string>(0));
    }

    private static Failure FailureWithReferenceDetail() => new()
    {
        Message = "boom",
        ApplicationFailureInfo = new()
        {
            Type = "MyError",
            Details = new()
            {
                Payloads_ =
                {
                    ExternalStorageReferences.CreateReferencePayload(
                        "mem", new Dictionary<string, string> { ["key"] = "k" }, originalSizeBytes: 1024),
                },
            },
        },
    };

    private sealed class FixedDriver : IStorageDriver
    {
        private readonly Payload payload;

        public FixedDriver(string name, Payload payload)
        {
            Name = name;
            this.payload = payload;
        }

        public string Name { get; }

        public string Type => "test.fixeddriver";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(
                claims.Select(_ => payload.Clone()).ToList());
    }

    private sealed class MapDriver : IStorageDriver
    {
        private readonly Dictionary<string, Payload> stored = new();

        public MapDriver(string name) => Name = name;

        public string Name { get; }

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
