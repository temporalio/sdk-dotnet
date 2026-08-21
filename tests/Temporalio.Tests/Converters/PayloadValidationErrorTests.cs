namespace Temporalio.Tests.Converters;

using Temporalio.Converters;
using Xunit;

public class PayloadValidationErrorTests
{
    [Fact]
    public void CreateException_WithDetails_CreatesExpectedApplicationFailure()
    {
        var details = new { Path = "name", Reason = "must not be empty" };

        var exception = PayloadValidationError.CreateException(details);

        Assert.Equal("Payload validation failed", exception.Message);
        Assert.Equal("PayloadValidationError", exception.ErrorType);
        Assert.True(exception.NonRetryable);
        Assert.Equal(1, exception.Details.Count);
        Assert.Same(details, exception.Details.ElementAt<object>(0));
    }

    [Fact]
    public void CreateException_WithNullDetails_CreatesApplicationFailureWithoutDetails()
    {
        var exception = PayloadValidationError.CreateException(null);

        Assert.Equal("Payload validation failed", exception.Message);
        Assert.Equal("PayloadValidationError", exception.ErrorType);
        Assert.True(exception.NonRetryable);
        Assert.Equal(0, exception.Details.Count);
    }
}
