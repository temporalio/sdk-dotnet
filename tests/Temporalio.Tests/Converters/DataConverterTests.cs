namespace Temporalio.Tests.Converters;

using System;
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
}
