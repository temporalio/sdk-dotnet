namespace Temporalio.Tests.Converters;

using System.Collections.Generic;
using NexusRpc.Handlers;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;
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
    public void ToFailure_HandlerException_WithOriginalFailure_PreservesOriginalFailureInfo()
    {
        // Simulate a round-trip: an ApplicationFailureInfo that was serialized to
        // NexusRpc.Failure and is now being restored
        var originalProto = new Failure()
        {
            Message = "Original error",
            StackTrace = "at SomeMethod()",
            ApplicationFailureInfo = new()
            {
                Type = "CustomError",
                NonRetryable = true,
            },
        };
        // Simulate what ExceptionToNexusFailureAsync used to do: serialize proto to JSON
        var message = originalProto.Message;
        originalProto.Message = string.Empty;
        var json = Google.Protobuf.JsonFormatter.Default.Format(originalProto);
        originalProto.Message = message;

        // Create a NexusRpc.Failure with the serialized proto in details
        var detailsDict = System.Text.Json.JsonSerializer.Deserialize<
            Dictionary<string, object>>(json)!;
        var nexusFailure = new NexusRpc.Failure(
            message: "Original error",
            metadata: new Dictionary<string, string>
            {
                ["type"] = Failure.Descriptor.FullName,
            },
            details: detailsDict,
            stackTrace: "at SomeMethod()");

        // Create a HandlerException with OriginalFailure set
        var handlerExc = new HandlerException(
            HandlerErrorType.Internal,
            "Handler wrapper message",
            innerException: null,
            errorRetryBehavior: HandlerErrorRetryBehavior.NonRetryable,
            originalFailure: nexusFailure);

        var failure = DataConverter.Default.FailureConverter.ToFailure(
            handlerExc, DataConverter.Default.PayloadConverter);

        // CRITICAL: The original ApplicationFailureInfo must be preserved, NOT overwritten
        // with NexusHandlerFailureInfo
        Assert.NotNull(failure.ApplicationFailureInfo);
        Assert.Equal("CustomError", failure.ApplicationFailureInfo.Type);
        Assert.True(failure.ApplicationFailureInfo.NonRetryable);
        Assert.Equal("Original error", failure.Message);
        Assert.Equal("at SomeMethod()", failure.StackTrace);
        // NexusHandlerFailureInfo should NOT be set (protobuf oneof)
        Assert.Null(failure.NexusHandlerFailureInfo);
    }

    [Fact]
    public void ToFailure_HandlerException_WithOriginalFailure_AppliesEncodeCommonAttributes()
    {
        // EncodeCommonAttributes applies uniformly to all failures including round-trip
        var nexusFailure = new NexusRpc.Failure(
            message: "Test message",
            metadata: new Dictionary<string, string>
            {
                ["type"] = Failure.Descriptor.FullName,
            },
            details: new Dictionary<string, object>(),
            stackTrace: "test stack");

        var handlerExc = new HandlerException(
            HandlerErrorType.Internal,
            "wrapper",
            innerException: null,
            originalFailure: nexusFailure);

        var converter = new DefaultFailureConverter.WithEncodedCommonAttributes();
        var failure = converter.ToFailure(
            handlerExc, DataConverter.Default.PayloadConverter);

        // EncodeCommonAttributes applies uniformly, including round-trip path
        Assert.Equal("Encoded failure", failure.Message);
        Assert.Empty(failure.StackTrace);
        Assert.NotNull(failure.EncodedAttributes);
    }

    [Fact]
    public void NexusFailureToTemporalFailure_NonTemporalFailure_ProducesNexusFailureType()
    {
        // A NexusRpc.Failure without the Temporal proto metadata should produce
        // an ApplicationFailureInfo with type "NexusFailure"
        var nexusFailure = new NexusRpc.Failure(
            message: "Some nexus error",
            metadata: new Dictionary<string, string> { ["custom"] = "value" },
            stackTrace: "remote stack");

        var failure = DefaultFailureConverter.NexusFailureToTemporalFailure(
            nexusFailure, nonRetryable: false);

        Assert.Equal("Some nexus error", failure.Message);
        Assert.Equal("remote stack", failure.StackTrace);
        Assert.NotNull(failure.ApplicationFailureInfo);
        Assert.Equal("NexusFailure", failure.ApplicationFailureInfo.Type);
        // nonRetryable parameter should be respected
        Assert.False(failure.ApplicationFailureInfo.NonRetryable);
        // Details payload should contain serialized NexusRpc.Failure
        Assert.NotNull(failure.ApplicationFailureInfo.Details);
        Assert.Single(failure.ApplicationFailureInfo.Details.Payloads_);
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
