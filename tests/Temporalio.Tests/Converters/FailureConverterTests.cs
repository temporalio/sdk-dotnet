namespace Temporalio.Tests.Converters;

using Temporalio.Converters;
using Temporalio.Exceptions;
using Xunit;
using Xunit.Abstractions;

public class FailureConverterTests : TestBase
{
    public FailureConverterTests(ITestOutputHelper output) : base(output) { }

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
            DataConverter.Default.PayloadConverter
        );
        Assert.Equal("exc1", failure.Message);
        Assert.Contains("FailureConverterTests", failure.StackTrace);
        Assert.Equal("ArgumentException", failure.ApplicationFailureInfo.Type);
        Assert.Equal("exc2", failure.Cause.Message);
        Assert.Equal("InvalidOperationException", failure.Cause.ApplicationFailureInfo.Type);

        // Add some details and confirm it properly deserializes too
        var exc = DataConverter.Default.FailureConverter.ToException(
            failure,
            DataConverter.Default.PayloadConverter
        );
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
            DataConverter.Default.PayloadConverter
        );
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
}
