namespace Temporalio.Tests.Client;

using Temporalio.Bridge;
using Temporalio.Client;
using Xunit;

public unsafe class TemporalConnectionOptionsTests
{
    [Fact]
    public void ToInteropOptions_AutoEnablesTls_WhenApiKeyProvidedAndTlsNotSet()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            ApiKey = "test-api-key",
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        // TLS should be auto-enabled when API key is provided and TLS not explicitly set
        Assert.True(interopOptions.tls_options != null);
    }

    [Fact]
    public void ToInteropOptions_RespectsExplicitTlsNull_WhenApiKeyProvided()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            ApiKey = "test-api-key",
            Tls = null, // Explicitly disable TLS
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        // TLS should remain disabled when explicitly set to null
        Assert.True(interopOptions.tls_options == null);
    }
}
