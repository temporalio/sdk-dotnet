namespace Temporalio.Tests.Converters;

using NexusRpc.Handlers;
using Temporalio.Api.Enums.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Xunit;
using Xunit.Abstractions;

public class FailureConverterTests : TestBase
{
    public FailureConverterTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    public void ToFailure_Common_Succeeds()
    {
        // Common exception. We have to throw it to get a stack trace.
        var throws = () =>
        {
            throw new ArgumentException("exc1", new InvalidOperationException("exc2"));
        };
        var failure = DataConverter.Default.FailureConverter.ToFailure(
            Assert.Throws<ArgumentException>(throws),
            DataConverter.Default.PayloadConverter);
        Assert.Equal("exc1", failure.Message);
        Assert.Contains("FailureConverterTests", failure.StackTrace);
        Assert.Equal("ArgumentException", failure.ApplicationFailureInfo.Type);
        Assert.Equal("exc2", failure.Cause.Message);
        Assert.Equal("InvalidOperationException", failure.Cause.ApplicationFailureInfo.Type);

        // Add some details and confirm it properly deserializes too
        var exc = DataConverter.Default.FailureConverter.ToException(
            failure,
            DataConverter.Default.PayloadConverter);
        Assert.IsType<ApplicationFailureException>(exc);
        Assert.Equal(failure.Message, exc.Message);
        Assert.Equal(failure.StackTrace, exc.StackTrace);
        Assert.IsType<ApplicationFailureException>(exc.InnerException);
        Assert.Equal(failure.Cause.Message, exc.InnerException.Message);
    }

    [Fact]
    public void ToFailure_EncodedAttributes_Succeeds()
    {
        var throws = () =>
        {
            throw new ArgumentException("exc1", new InvalidOperationException("exc2"));
        };
        var converter = new DefaultFailureConverter.WithEncodedCommonAttributes();
        var failure = converter.ToFailure(
            Assert.Throws<ArgumentException>(throws),
            DataConverter.Default.PayloadConverter);
        Assert.Equal("Encoded failure", failure.Message);
        Assert.Empty(failure.StackTrace);
        Assert.Equal("ArgumentException", failure.ApplicationFailureInfo.Type);
        Assert.Equal("Encoded failure", failure.Cause.Message);
        Assert.Equal("InvalidOperationException", failure.Cause.ApplicationFailureInfo.Type);

        var exc = converter.ToException(failure, DataConverter.Default.PayloadConverter);
        Assert.Equal("exc1", exc.Message);
        Assert.Contains("FailureConverterTests", exc.StackTrace);
        Assert.Equal("exc2", exc.InnerException!.Message);
    }

    [Fact]
    public void ToFailure_HandlerException_FreshError_ProducesNexusHandlerFailureInfo()
    {
        // A fresh HandlerException (no OriginalFailure) should produce a Failure with
        // NexusHandlerFailureInfo
        var handlerExc = new HandlerException(
            HandlerErrorType.BadRequest,
            "Bad input",
            new InvalidOperationException("inner cause"),
            HandlerErrorRetryBehavior.NonRetryable);
        var failure = DataConverter.Default.FailureConverter.ToFailure(
            handlerExc, DataConverter.Default.PayloadConverter);

        Assert.Equal("Bad input", failure.Message);
        Assert.NotNull(failure.NexusHandlerFailureInfo);
        Assert.Equal("BAD_REQUEST", failure.NexusHandlerFailureInfo.Type);
        Assert.Equal(
            NexusHandlerErrorRetryBehavior.NonRetryable,
            failure.NexusHandlerFailureInfo.RetryBehavior);
        // Inner exception serialized as Cause
        Assert.NotNull(failure.Cause);
        Assert.Equal("inner cause", failure.Cause.Message);
        Assert.Equal(
            "InvalidOperationException", failure.Cause.ApplicationFailureInfo.Type);
    }

    [Fact]
    public void ToFailure_HandlerException_FreshError_RoundTripsViaToException()
    {
        var handlerExc = new HandlerException(
            HandlerErrorType.Internal,
            "Server error",
            errorRetryBehavior: HandlerErrorRetryBehavior.Retryable);
        var failure = DataConverter.Default.FailureConverter.ToFailure(
            handlerExc, DataConverter.Default.PayloadConverter);
        var exc = DataConverter.Default.FailureConverter.ToException(
            failure, DataConverter.Default.PayloadConverter);

        var nexusExc = Assert.IsType<HandlerException>(exc);
        Assert.Equal(HandlerErrorType.Internal, nexusExc.ErrorType);
        Assert.Equal("INTERNAL", nexusExc.RawErrorType);
        Assert.Equal(
            HandlerErrorRetryBehavior.Retryable,
            nexusExc.ErrorRetryBehavior);
        Assert.Equal("Server error", nexusExc.Message);
        Assert.Null(nexusExc.InnerException);
        // OriginalFailure should be set for round-tripping
        Assert.NotNull(nexusExc.OriginalFailure);
    }

    [Fact]
    public void ToFailure_HandlerException_WithOriginalFailure_RoundTripsCorrectly()
    {
        // Start from what a user would actually throw in a handler
        var userException = new HandlerException(
            HandlerErrorType.Internal,
            "Handler error message",
            new ApplicationFailureException("Root cause", errorType: "CustomError", nonRetryable: true),
            HandlerErrorRetryBehavior.NonRetryable);

        // Serialize to Failure proto (handler side)
        var failureProto = DataConverter.Default.FailureConverter.ToFailure(
            userException, DataConverter.Default.PayloadConverter);

        // Deserialize back to exception (caller side) — this produces a HandlerException
        // with OriginalFailure set for round-tripping
        var deserialized = DataConverter.Default.FailureConverter.ToException(
            failureProto, DataConverter.Default.PayloadConverter);
        var handlerExc = Assert.IsType<HandlerException>(deserialized);
        Assert.NotNull(handlerExc.OriginalFailure);

        // Now re-serialize (simulates the round-trip through another Nexus boundary)
        var roundTripped = DataConverter.Default.FailureConverter.ToFailure(
            handlerExc, DataConverter.Default.PayloadConverter);

        // The round-tripped proto should preserve the full structure
        Assert.NotNull(roundTripped.NexusHandlerFailureInfo);
        Assert.Equal("INTERNAL", roundTripped.NexusHandlerFailureInfo.Type);
        Assert.Equal("Handler error message", roundTripped.Message);
        Assert.NotNull(roundTripped.Cause);
        Assert.Equal("Root cause", roundTripped.Cause.Message);
        Assert.NotNull(roundTripped.Cause.ApplicationFailureInfo);
        Assert.Equal("CustomError", roundTripped.Cause.ApplicationFailureInfo.Type);
    }

    [Fact]
    public void ToFailure_HandlerException_WithOriginalFailure_AppliesEncodeCommonAttributes()
    {
        var userException = new HandlerException(
            HandlerErrorType.Internal,
            "Handler error",
            errorRetryBehavior: HandlerErrorRetryBehavior.NonRetryable);

        // First pass: serialize and deserialize to get a HandlerException with OriginalFailure
        var firstProto = DataConverter.Default.FailureConverter.ToFailure(
            userException, DataConverter.Default.PayloadConverter);
        var deserialized = Assert.IsType<HandlerException>(
            DataConverter.Default.FailureConverter.ToException(
                firstProto, DataConverter.Default.PayloadConverter));
        Assert.NotNull(deserialized.OriginalFailure);

        // Second pass: re-serialize with EncodeCommonAttributes
        var converter = new DefaultFailureConverter.WithEncodedCommonAttributes();
        var failure = converter.ToFailure(
            deserialized, DataConverter.Default.PayloadConverter);

        // EncodeCommonAttributes applies uniformly, including round-trip path
        Assert.Equal("Encoded failure", failure.Message);
        Assert.Empty(failure.StackTrace);
        Assert.NotNull(failure.EncodedAttributes);
    }

    [Fact]
    public void ToFailure_HandlerException_RetryableAppFailure_SetsRetryable()
    {
        // Verify that a retryable ApplicationFailureException wrapped in a HandlerException
        // gets Retryable retry behavior (not Unspecified)
        var appExc = new ApplicationFailureException("retryable error");
        // Simulate ConvertToHandlerException for retryable case
        var handlerExc = new HandlerException(
            HandlerErrorType.Internal,
            "Handler failed with application error",
            appExc,
            HandlerErrorRetryBehavior.Retryable);

        var failure = DataConverter.Default.FailureConverter.ToFailure(
            handlerExc, DataConverter.Default.PayloadConverter);

        Assert.NotNull(failure.NexusHandlerFailureInfo);
        Assert.Equal(
            NexusHandlerErrorRetryBehavior.Retryable,
            failure.NexusHandlerFailureInfo.RetryBehavior);
    }
}
