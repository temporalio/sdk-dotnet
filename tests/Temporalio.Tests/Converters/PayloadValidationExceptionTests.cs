namespace Temporalio.Tests.Converters;

using Temporalio.Converters;
using Xunit;

public class PayloadValidationExceptionTests
{
    [Fact]
    public void Create_WithDetails_CreatesExpectedApplicationFailure()
    {
        var details = new { Path = "name", Reason = "must not be empty" };

        var exception = PayloadValidationException.Create(details);

        Assert.Equal("Payload validation failed", exception.Message);
        Assert.Equal("PayloadValidationError", exception.ErrorType);
        Assert.True(exception.NonRetryable);
        Assert.Equal(1, exception.Details.Count);
        Assert.Same(details, exception.Details.ElementAt<object>(0));
    }

    [Fact]
    public void Create_WithNullDetails_CreatesApplicationFailureWithoutDetails()
    {
        var exception = PayloadValidationException.Create(null);

        Assert.Equal("Payload validation failed", exception.Message);
        Assert.Equal("PayloadValidationError", exception.ErrorType);
        Assert.True(exception.NonRetryable);
        Assert.Equal(0, exception.Details.Count);
    }
}
