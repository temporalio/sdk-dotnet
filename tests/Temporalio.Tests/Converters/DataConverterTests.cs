namespace Temporalio.Tests.Converters;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Xunit;
using Xunit.Abstractions;

public class DataConverterTests : TestBase
{
    public DataConverterTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void NewDataConverter_WithPayloadConverter_ProperlyInitializes()
    {
        var payloadConverter = new MyPayloadConverter();
        var newConverter = DataConverter.Default with
        {
            PayloadConverter = payloadConverter,
        };
        Assert.Same(payloadConverter, newConverter.PayloadConverter);
        Assert.Equal(
            "payload",
            newConverter.PayloadConverter.ToValue(
                newConverter.PayloadConverter.ToPayload("payload"), typeof(string)));
    }

    [Fact]
    public void NewDataConverter_EquivalentConverters_AreEqual()
    {
        var payloadConverter = new MyPayloadConverter();
        var failureConverter = new DefaultFailureConverter();

        Assert.Equal(
            new DataConverter(payloadConverter, failureConverter),
            new DataConverter(payloadConverter, failureConverter));
    }

    [Fact]
    public void WithSerializationContext_WithExternalStorage_PreservesExternalStorage()
    {
        var storage = new ExternalStorage(new StubStorageDriver());
        var converter = DataConverter.Default with
        {
            PayloadConverter = new ContextAwarePayloadConverter(),
            ExternalStorage = storage,
        };

        var scoped = converter.WithSerializationContext(
            new ISerializationContext.Workflow("default", "workflow-id"));

        // The context-aware payload converter forces a new instance, so this exercises the copy path
        // rather than the return-this short-circuit.
        Assert.NotSame(converter, scoped);
        Assert.Same(storage, scoped.ExternalStorage);
    }

    [Fact]
    public void WithSerializationContext_NoContextAwareConverters_ReturnsSame()
    {
        var converter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(new StubStorageDriver()),
        };
        Assert.Same(
            converter,
            converter.WithSerializationContext(
                new ISerializationContext.Workflow("default", "workflow-id")));
    }

    public class MyPayloadConverter : IPayloadConverter
    {
        public Payload ToPayload(object? value) => new()
        {
            Metadata =
            {
                ["encoding"] = ByteString.CopyFromUtf8("test/plain"),
            },
            Data = ByteString.CopyFromUtf8((string)value!),
        };

        public object? ToValue(Payload payload, Type type) => payload.Data.ToStringUtf8();
    }

    private class ContextAwarePayloadConverter
        : IPayloadConverter, IWithSerializationContext<IPayloadConverter>
    {
        public Payload ToPayload(object? value) => new()
        {
            Data = ByteString.CopyFromUtf8((string)value!),
        };

        public object? ToValue(Payload payload, Type type) => payload.Data.ToStringUtf8();

        public IPayloadConverter WithSerializationContext(ISerializationContext context) =>
            new ContextAwarePayloadConverter();
    }

    private class StubStorageDriver : IStorageDriver
    {
        public string Name => "stub";

        public string Type => "test.stubdriver";

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
