namespace Temporalio.Tests.Nexus;

using System;
using System.Threading.Tasks;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Temporalio.Nexus.Notifications;
using Temporalio.Tests.Converters;
using Temporalio.Worker;
using Xunit;
using ProtoOnCompleteRequest = Temporalio.Api.NotificationService.V1.OnCompleteRequest;
using ProtoOnCompleteResponse = Temporalio.Api.NotificationService.V1.OnCompleteResponse;

public class NotificationTests
{
    private static readonly SystemNexusPayloadConverter Converter = new(
        DataConverter.Default.PayloadConverter,
        DataConverter.Default.FailureConverter);

    [Fact]
    public void OnCompleteRequest_Success_RoundTripsTypedResultAndContext()
    {
        var request = new OnCompleteRequest<OperationOutput, NotificationContext>(
            new OnCompleteRequestResult<OperationOutput>.Success(new OperationOutput("finished")),
            new NotificationContext("source"));

        var payload = Converter.ToPayload(request);
        var proto = ProtoOnCompleteRequest.Parser.ParseFrom(payload.Data);
        Assert.Equal("binary/protobuf", payload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal(ProtoOnCompleteRequest.ResultOneofCase.Success, proto.ResultCase);
        Assert.NotNull(proto.SourceContext);
        Assert.Equal(
            new OperationOutput("finished"),
            DataConverter.Default.PayloadConverter.ToValue(proto.Success, typeof(OperationOutput)));
        Assert.Equal(
            new NotificationContext("source"),
            DataConverter.Default.PayloadConverter.ToValue(proto.SourceContext, typeof(NotificationContext)));

        var restored = Assert.IsType<OnCompleteRequest<OperationOutput, NotificationContext>>(
            Converter.ToValue(payload, typeof(OnCompleteRequest<OperationOutput, NotificationContext>)));
        var success = Assert.IsType<OnCompleteRequestResult<OperationOutput>.Success>(restored.Result);
        Assert.Equal(new OperationOutput("finished"), success.Value);
        Assert.Equal(new NotificationContext("source"), restored.SourceContext);
    }

    [Fact]
    public void OnCompleteRequest_Failure_RoundTripsApplicationFailureDetails()
    {
        var request = new OnCompleteRequest<OperationOutput, NotificationContext>(
            new OnCompleteRequestResult<OperationOutput>.Failure(
                new ApplicationFailureException(
                    "failed",
                    errorType: "OperationError",
                    nonRetryable: true,
                    details: new object?[] { "diagnostic" })),
            new NotificationContext("source"));

        var payload = Converter.ToPayload(request);
        var proto = ProtoOnCompleteRequest.Parser.ParseFrom(payload.Data);
        Assert.Equal(ProtoOnCompleteRequest.ResultOneofCase.Failure, proto.ResultCase);
        Assert.Equal("OperationError", proto.Failure.ApplicationFailureInfo.Type);
        Assert.Single(proto.Failure.ApplicationFailureInfo.Details.Payloads_);
        var restored = Assert.IsType<OnCompleteRequest<OperationOutput, NotificationContext>>(
            Converter.ToValue(payload, typeof(OnCompleteRequest<OperationOutput, NotificationContext>)));
        var failure = Assert.IsType<OnCompleteRequestResult<OperationOutput>.Failure>(restored.Result);
        var exception = Assert.IsType<ApplicationFailureException>(failure.Value);
        Assert.Equal("failed", exception.Message);
        Assert.Equal("OperationError", exception.ErrorType);
        Assert.True(exception.NonRetryable);
        Assert.Equal("diagnostic", exception.Details.ElementAt<string>(0));
        Assert.Equal(new NotificationContext("source"), restored.SourceContext);
    }

    [Fact]
    public void OnCompleteResponse_Empty_RoundTrips()
    {
        var payload = Converter.ToPayload(new OnCompleteResponse());
        Assert.Equal("binary/protobuf", payload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal(ProtoOnCompleteResponse.Descriptor.FullName, payload.Metadata["messageType"].ToStringUtf8());
        Assert.Equal(0, ProtoOnCompleteResponse.Parser.ParseFrom(payload.Data).CalculateSize());
        Assert.IsType<OnCompleteResponse>(Converter.ToValue(payload, typeof(OnCompleteResponse)));
    }

    [Fact]
    public void OnCompleteRequest_MissingResult_IsRejected()
    {
        var proto = new ProtoOnCompleteRequest
        {
            SourceContext = DataConverter.Default.PayloadConverter.ToPayload(new NotificationContext("source")),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var payload = Converter.ToPayload(proto);
            Converter.ToValue(payload, typeof(OnCompleteRequest<OperationOutput, NotificationContext>));
        });
        Assert.Equal("missing required field OnCompleteRequest.Result", exception.Message);
    }

    [Fact]
    public void OnCompleteRequest_MissingSourceContext_IsRejected()
    {
        var proto = new ProtoOnCompleteRequest
        {
            Success = DataConverter.Default.PayloadConverter.ToPayload(new OperationOutput("finished")),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var payload = Converter.ToPayload(proto);
            Converter.ToValue(payload, typeof(OnCompleteRequest<OperationOutput, NotificationContext>));
        });
        Assert.Equal("missing required field OnCompleteRequest.SourceContext", exception.Message);
    }

    [Fact]
    public async Task OnCompleteRequest_Success_EmbeddedPayloadsAreCodecVisited()
    {
        var request = new OnCompleteRequest<OperationOutput, NotificationContext>(
            new OnCompleteRequestResult<OperationOutput>.Success(new OperationOutput("finished")),
            new NotificationContext("source"));
        var payload = Converter.ToPayload(request);
        var codec = new Base64PayloadCodec();

        await PayloadCodecHelper.EncodeAsync(codec, payload);
        var encoded = ProtoOnCompleteRequest.Parser.ParseFrom(payload.Data);
        Assert.Equal("binary/protobuf", payload.Metadata["encoding"].ToStringUtf8());
        Assert.Equal(Base64PayloadCodec.EncodingName, encoded.Success.Metadata["encoding"].ToStringUtf8());
        Assert.Equal(Base64PayloadCodec.EncodingName, encoded.SourceContext.Metadata["encoding"].ToStringUtf8());

        await PayloadCodecHelper.DecodeAsync(codec, payload);
        var restored = Assert.IsType<OnCompleteRequest<OperationOutput, NotificationContext>>(
            Converter.ToValue(payload, typeof(OnCompleteRequest<OperationOutput, NotificationContext>)));
        Assert.Equal(
            new OperationOutput("finished"),
            Assert.IsType<OnCompleteRequestResult<OperationOutput>.Success>(restored.Result).Value);
        Assert.Equal(new NotificationContext("source"), restored.SourceContext);
    }

    [Fact]
    public async Task OnCompleteRequest_Failure_DetailPayloadsAreCodecVisited()
    {
        var request = new OnCompleteRequest<OperationOutput, NotificationContext>(
            new OnCompleteRequestResult<OperationOutput>.Failure(
                new ApplicationFailureException(
                    "failed", errorType: "OperationError", details: new object?[] { "diagnostic" })),
            new NotificationContext("source"));
        var payload = Converter.ToPayload(request);
        var codec = new Base64PayloadCodec();

        await PayloadCodecHelper.EncodeAsync(codec, payload);
        var encoded = ProtoOnCompleteRequest.Parser.ParseFrom(payload.Data);
        Assert.Equal(
            Base64PayloadCodec.EncodingName,
            encoded.Failure.ApplicationFailureInfo.Details.Payloads_[0].Metadata["encoding"].ToStringUtf8());
        Assert.Equal(Base64PayloadCodec.EncodingName, encoded.SourceContext.Metadata["encoding"].ToStringUtf8());

        await PayloadCodecHelper.DecodeAsync(codec, payload);
        var restored = Assert.IsType<OnCompleteRequest<OperationOutput, NotificationContext>>(
            Converter.ToValue(payload, typeof(OnCompleteRequest<OperationOutput, NotificationContext>)));
        var exception = Assert.IsType<ApplicationFailureException>(
            Assert.IsType<OnCompleteRequestResult<OperationOutput>.Failure>(restored.Result).Value);
        Assert.Equal("diagnostic", exception.Details.ElementAt<string>(0));
        Assert.Equal(new NotificationContext("source"), restored.SourceContext);
    }

    public sealed record OperationOutput(string Value);

    public sealed record NotificationContext(string Value);
}
