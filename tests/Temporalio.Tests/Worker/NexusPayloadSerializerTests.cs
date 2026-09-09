namespace Temporalio.Tests.Worker;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using NexusRpc;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;

/// <summary>
/// Server-independent unit tests for Nexus payload decoding and converter failure translation.
/// </summary>
public class NexusPayloadSerializerTests
{
    [Fact]
    public async Task DeserializeAsync_SystemPayload_UsesSystemConverter()
    {
        var dataConverter = DataConverter.Default;
        var request = new SignalWithStartWorkflowRequest(
            workflow: "test-workflow",
            id: "target-workflow-id",
            taskQueue: "target-task-queue",
            signal: "test-signal",
            @namespace: "target-namespace")
        {
            Args = new object?[] { "workflow-input" },
        };
        var payload = new SystemNexusPayloadConverter(
            dataConverter.PayloadConverter, dataConverter.FailureConverter).ToPayload(request);
        Assert.True(SystemNexusPayloadVisitor.IsSystemPayload(payload));

        var result = Assert.IsType<SignalWithStartWorkflowRequest>(
            await new NexusPayloadSerializer(dataConverter).DeserializeAsync(
                new(payload.ToByteArray()), typeof(SignalWithStartWorkflowRequest)));

        Assert.Equal("workflow-input", Assert.Single(result.Args!)?.ToString());
    }

    [Fact]
    public async Task DeserializeAsync_SystemPayload_AppliesTransferTypeConverter()
    {
        var transferValue = new WorkflowType { Name = "test-value" };
        Assert.True(new BinaryProtoConverter().TryToPayload(transferValue, out var payload));
        SystemNexusPayloadVisitor.MarkSystemPayload(payload!);

        var result = Assert.IsType<TestSystemRequest>(
            await new NexusPayloadSerializer(DataConverter.Default).DeserializeAsync(
                new(payload!.ToByteArray()), typeof(TestSystemRequest)));

        Assert.Equal(new TestSystemRequest("test-value"), result);
    }

    [TemporalTransferTypeConverter(typeof(TestSystemRequestConverter))]
    public sealed record TestSystemRequest(string Value);

    public sealed class TestSystemRequestConverter : ITemporalTransferTypeConverter
    {
        public Type TransferType => typeof(WorkflowType);

        public object ToTransferType(object? value) =>
            new WorkflowType { Name = ((TestSystemRequest)value!).Value };

        public object FromTransferType(object? transferType) =>
            new TestSystemRequest(((WorkflowType)transferType!).Name);
    }

    [Fact]
    public async Task DeserializeAsync_SystemPayloadWithCodec_DecodesNestedPayloads()
    {
        var codec = new ReplacingPayloadCodec();
        var dataConverter = DataConverter.Default with { PayloadCodec = codec };
        var request = new SignalWithStartWorkflowRequest(
            workflow: "test-workflow",
            id: "target-workflow-id",
            taskQueue: "target-task-queue",
            signal: "test-signal",
            @namespace: "target-namespace")
        {
            Args = new object?[] { "workflow-input" },
        };
        var payload = new SystemNexusPayloadConverter(
            dataConverter.PayloadConverter, dataConverter.FailureConverter).ToPayload(request);

        var result = Assert.IsType<SignalWithStartWorkflowRequest>(
            await new NexusPayloadSerializer(dataConverter).DeserializeAsync(
                new(payload.ToByteArray()), typeof(SignalWithStartWorkflowRequest)));

        Assert.Equal(1, codec.DecodeCount);
        Assert.Equal(0, codec.EncodeCount);
        Assert.Equal("decoded-input", Assert.Single(result.Args!)?.ToString());
    }

    private class ReplacingPayloadCodec : IPayloadCodec
    {
        public int DecodeCount { get; private set; }

        public int EncodeCount { get; private set; }

        public Task<IReadOnlyCollection<Payload>> EncodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            EncodeCount++;
            return Task.FromResult(payloads);
        }

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(
            IReadOnlyCollection<Payload> payloads)
        {
            Assert.All(payloads, payload =>
                Assert.False(SystemNexusPayloadVisitor.IsSystemPayload(payload)));
            DecodeCount++;
            return Task.FromResult<IReadOnlyCollection<Payload>>(new[]
            {
                DataConverter.Default.PayloadConverter.ToPayload("decoded-input"),
            });
        }
    }

    [Fact]
    public async Task DeserializeAsync_UnmarkedPayload_UsesUserConverter()
    {
        var payload = DataConverter.Default.PayloadConverter.ToPayload("ordinary-input");
        Assert.False(SystemNexusPayloadVisitor.IsSystemPayload(payload));

        var result = await new NexusPayloadSerializer(DataConverter.Default).DeserializeAsync(
            new(payload.ToByteArray()), typeof(string));

        Assert.Equal("ordinary-input", result);
    }

    [Fact]
    public async Task DeserializeAsync_ConverterPayloadValidationFailure_BecomesBadRequest()
    {
        var cause = PayloadValidationError.CreateException(new { Reason = "invalid input" });
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadConverter = new ThrowingPayloadConverter(cause),
        });
        var content = await CreateContentAsync("some-input");

        var exc = await Assert.ThrowsAsync<HandlerException>(
            () => serializer.DeserializeAsync(content, typeof(string)));
        Assert.Equal(HandlerErrorType.BadRequest, exc.ErrorType);
        Assert.Equal(HandlerErrorRetryBehavior.NonRetryable, exc.ErrorRetryBehavior);
        // A validation failure gets its own message, distinct from the generic decode failure
        Assert.Equal("Invalid operation input", exc.Message);
        Assert.Same(cause, exc.InnerException);
        var inner = Assert.IsType<ApplicationFailureException>(exc.InnerException);
        Assert.Equal("Payload validation failed", inner.Message);
        Assert.Equal("PayloadValidationError", inner.ErrorType);
        Assert.True(inner.NonRetryable);
    }

    [Fact]
    public async Task DeserializeAsync_CodecPayloadValidationFailure_BecomesBadRequest()
    {
        var cause = PayloadValidationError.CreateException(new { Reason = "invalid input" });
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadCodec = new ThrowingPayloadCodec(cause),
        });
        var content = await CreateContentAsync("some-input");

        var exc = await Assert.ThrowsAsync<HandlerException>(
            () => serializer.DeserializeAsync(content, typeof(string)));
        Assert.Equal(HandlerErrorType.BadRequest, exc.ErrorType);
        Assert.Equal(HandlerErrorRetryBehavior.NonRetryable, exc.ErrorRetryBehavior);
        // A validation failure gets its own message, distinct from the generic decode failure
        Assert.Equal("Invalid operation input", exc.Message);
        Assert.Same(cause, exc.InnerException);
        var inner = Assert.IsType<ApplicationFailureException>(exc.InnerException);
        Assert.Equal("Payload validation failed", inner.Message);
        Assert.Equal("PayloadValidationError", inner.ErrorType);
        Assert.True(inner.NonRetryable);
    }

    [Fact]
    public async Task DeserializeAsync_ConverterOtherFailure_KeepsGenericDecodeMessage()
    {
        var cause = new InvalidOperationException("Simulated converter failure");
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadConverter = new ThrowingPayloadConverter(cause),
        });
        var content = await CreateContentAsync("some-input");

        var exc = await Assert.ThrowsAsync<HandlerException>(
            () => serializer.DeserializeAsync(content, typeof(string)));
        Assert.Equal(HandlerErrorType.BadRequest, exc.ErrorType);
        Assert.Equal(HandlerErrorRetryBehavior.NonRetryable, exc.ErrorRetryBehavior);
        Assert.Equal("Payload converter failed to decode Nexus operation input", exc.Message);
        Assert.Same(cause, exc.InnerException);
    }

    [Fact]
    public async Task DeserializeAsync_CodecOtherFailure_KeepsGenericDecodeMessage()
    {
        var cause = new InvalidOperationException("Simulated codec failure");
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadCodec = new ThrowingPayloadCodec(cause),
        });
        var content = await CreateContentAsync("some-input");

        var exc = await Assert.ThrowsAsync<HandlerException>(
            () => serializer.DeserializeAsync(content, typeof(string)));
        Assert.Equal(HandlerErrorType.Internal, exc.ErrorType);
        Assert.Equal("Payload codec failed to decode Nexus operation input", exc.Message);
        Assert.Same(cause, exc.InnerException);
    }

    // Only a non-retryable failure with the exact reserved error type is a bad request, everything
    // else is passed through untouched for the regular error handling path to convert.
    [Theory]
    [InlineData(false, "PayloadValidationError")]
    [InlineData(true, "SomeOtherError")]
    [InlineData(false, "SomeOtherError")]
    [InlineData(true, null)]
    public async Task DeserializeAsync_ConverterOtherApplicationFailure_IsPassedThrough(
        bool nonRetryable, string? errorType)
    {
        var cause = new ApplicationFailureException(
            "Intentional failure", errorType: errorType, nonRetryable: nonRetryable);
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadConverter = new ThrowingPayloadConverter(cause),
        });
        var content = await CreateContentAsync("some-input");

        var exc = await Assert.ThrowsAsync<ApplicationFailureException>(
            () => serializer.DeserializeAsync(content, typeof(string)));
        Assert.Same(cause, exc);
    }

    [Theory]
    [InlineData(false, "PayloadValidationError")]
    [InlineData(true, "SomeOtherError")]
    [InlineData(false, "SomeOtherError")]
    [InlineData(true, null)]
    public async Task DeserializeAsync_CodecOtherApplicationFailure_IsPassedThrough(
        bool nonRetryable, string? errorType)
    {
        var cause = new ApplicationFailureException(
            "Intentional failure", errorType: errorType, nonRetryable: nonRetryable);
        var serializer = new NexusPayloadSerializer(DataConverter.Default with
        {
            PayloadCodec = new ThrowingPayloadCodec(cause),
        });
        var content = await CreateContentAsync("some-input");

        var exc = await Assert.ThrowsAsync<ApplicationFailureException>(
            () => serializer.DeserializeAsync(content, typeof(string)));
        Assert.Same(cause, exc);
    }

    private static async Task<ISerializer.Content> CreateContentAsync(string value)
    {
        var payload = await DataConverter.Default.ToPayloadAsync(value);
        return new(payload.ToByteArray());
    }

    private class ThrowingPayloadConverter : IPayloadConverter
    {
        private readonly IPayloadConverter inner = DataConverter.Default.PayloadConverter;
        private readonly Exception exception;

        public ThrowingPayloadConverter(Exception exception) => this.exception = exception;

        public Payload ToPayload(object? value) => inner.ToPayload(value);

        public object? ToValue(Payload payload, Type type) => throw exception;
    }

    private class ThrowingPayloadCodec : IPayloadCodec
    {
        private readonly Exception exception;

        public ThrowingPayloadCodec(Exception exception) => this.exception = exception;

        public Task<IReadOnlyCollection<Payload>> EncodeAsync(
            IReadOnlyCollection<Payload> payloads) => Task.FromResult(payloads);

        public Task<IReadOnlyCollection<Payload>> DecodeAsync(
            IReadOnlyCollection<Payload> payloads) => throw exception;
    }
}
