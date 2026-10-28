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
}
