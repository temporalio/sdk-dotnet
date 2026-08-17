namespace Temporalio.Tests.Nexus;

using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Nexus;
using Xunit;
using ApiWorkflowService = Temporalio.Api.WorkflowService.V1;

public class SystemNexusPayloadConverterTests
{
    [Fact]
    public void TransferTypeWithEmbeddedPayload_RoundTrips()
    {
        var userPayloadConverter = TemporalTransferTypePayloadConverter.Wrap(
            new StringPayloadConverter());
        var converter = new SystemNexusPayloadConverter(
            userPayloadConverter, new DefaultFailureConverter());
        var value = new SystemNexusRequest("embedded-value");

        var payload = converter.ToPayload(value);

        Assert.Equal("binary/protobuf", payload.Metadata["encoding"].ToStringUtf8());
        var transferType = ApiWorkflowService.SignalWithStartWorkflowExecutionRequest.Parser.ParseFrom(
            payload.Data);
        Assert.Equal("test/string", transferType.Input.Payloads_[0].Metadata["encoding"].ToStringUtf8());
        Assert.Equal(value, converter.ToValue(payload, typeof(SystemNexusRequest)));
        Assert.Throws<InvalidOperationException>(() =>
            _ = SystemNexusConverterContext.PayloadConverter);
    }

    [TemporalTransferTypeConverter(typeof(SystemNexusRequestConverter))]
    public sealed record SystemNexusRequest(string EmbeddedValue);

    public sealed class SystemNexusRequestConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(ApiWorkflowService.SignalWithStartWorkflowExecutionRequest);

        public object ToTransferType(object? value)
        {
            Assert.IsType<TemporalTransferTypePayloadConverter>(
                SystemNexusConverterContext.PayloadConverter);
            Assert.IsType<DefaultFailureConverter>(SystemNexusConverterContext.FailureConverter);
            var request = (SystemNexusRequest)value!;
            var input = new Payloads();
            input.Payloads_.Add(SystemNexusConverterContext.PayloadConverter.ToPayload(
                new EmbeddedValue(request.EmbeddedValue)));
            return new ApiWorkflowService.SignalWithStartWorkflowExecutionRequest { Input = input };
        }

        public object FromTransferType(object? transferType)
        {
            Assert.IsType<TemporalTransferTypePayloadConverter>(
                SystemNexusConverterContext.PayloadConverter);
            Assert.IsType<DefaultFailureConverter>(SystemNexusConverterContext.FailureConverter);
            var request = (ApiWorkflowService.SignalWithStartWorkflowExecutionRequest)transferType!;
            var embedded = SystemNexusConverterContext.PayloadConverter.ToValue<EmbeddedValue>(
                request.Input.Payloads_[0]);
            return new SystemNexusRequest(embedded.Value);
        }
    }

    [TemporalTransferTypeConverter(typeof(EmbeddedValueConverter))]
    public sealed record EmbeddedValue(string Value);

    public sealed class EmbeddedValueConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(string);

        public object ToTransferType(object? value) => ((EmbeddedValue)value!).Value;

        public object FromTransferType(object? transferType) => new EmbeddedValue((string)transferType!);
    }

    private sealed class StringPayloadConverter : IPayloadConverter
    {
        public Payload ToPayload(object? value)
        {
            var stringValue = Assert.IsType<string>(value);
            return new()
            {
                Metadata = { ["encoding"] = ByteString.CopyFromUtf8("test/string") },
                Data = ByteString.CopyFromUtf8(stringValue),
            };
        }

        public object? ToValue(Payload payload, Type type)
        {
            Assert.Equal(typeof(string), type);
            Assert.Equal("test/string", payload.Metadata["encoding"].ToStringUtf8());
            return payload.Data.ToStringUtf8();
        }
    }
}
