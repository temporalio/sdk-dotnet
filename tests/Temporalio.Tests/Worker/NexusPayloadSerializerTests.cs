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
using Temporalio.Worker;
using Xunit;

/// <summary>
/// Server-independent unit tests for how <see cref="NexusPayloadSerializer"/> translates data
/// converter failures into Nexus handler errors.
/// </summary>
public class NexusPayloadSerializerTests
{
    [Fact]
    public async Task DeserializeAsync_ConverterPayloadValidationFailure_BecomesBadRequest()
    {
        var cause = PayloadValidationException.Create(new { Reason = "invalid input" });
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
        var cause = PayloadValidationException.Create(new { Reason = "invalid input" });
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
