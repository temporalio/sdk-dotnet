namespace Temporalio.Tests.Converters;

using System;
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
    public void NewDataConverter_WithPayloadConverterType_ProperlyInitializes()
    {
        var newConverter = DataConverter.Default with
        {
            PayloadConverterType = typeof(MyPayloadConverter),
        };
        Assert.IsType<MyPayloadConverter>(newConverter.PayloadConverter);
    }

    public class MyPayloadConverter : IPayloadConverter
    {
        public Payload ToPayload(object? value) => throw new NotImplementedException();

        public object? ToValue(Payload payload, Type type) => throw new NotImplementedException();
    }
}
