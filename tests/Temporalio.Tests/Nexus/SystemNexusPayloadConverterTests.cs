namespace Temporalio.Tests.Nexus;

using Temporalio.Converters;
using Temporalio.Nexus;
using Xunit;
using ApiCommon = Temporalio.Api.Common.V1;

public class SystemNexusPayloadConverterTests
{
    [Fact]
    public void TransferType_RoundTrips()
    {
        var converter = new SystemNexusPayloadConverter(
            DataConverter.Default.PayloadConverter,
            DataConverter.Default.FailureConverter);
        var value = new SystemNexusRequest("value");

        var payload = converter.ToPayload(value);

        Assert.Equal("binary/protobuf", payload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal("value", ApiCommon.WorkflowType.Parser.ParseFrom(payload.Data).Name);
        Assert.Equal(value, converter.ToValue(payload, typeof(SystemNexusRequest)));
    }

    [TemporalTransferTypeConverter(typeof(SystemNexusRequestConverter))]
    public sealed record SystemNexusRequest(string Value);

    public sealed class SystemNexusRequestConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(ApiCommon.WorkflowType);

        public object ToTransferType(object? value) =>
            new ApiCommon.WorkflowType { Name = ((SystemNexusRequest)value!).Value };

        public object FromTransferType(object? transferType) =>
            new SystemNexusRequest(((ApiCommon.WorkflowType)transferType!).Name);
    }
}
