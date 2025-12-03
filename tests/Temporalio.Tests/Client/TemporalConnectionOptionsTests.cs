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
            Identity = "test-identity",
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        // TLS should be auto-enabled when API key is provided and TLS not set
        Assert.True(interopOptions.tls_options != null);
    }

    [Fact]
    public void ToInteropOptions_TlsDisabled_WhenExplicitlyDisabledWithApiKey()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            ApiKey = "test-api-key",
            Identity = "test-identity",
            Tls = new TlsOptions { Disabled = true },
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        // TLS should remain disabled when explicitly disabled, even with API key
        Assert.True(interopOptions.tls_options == null);
    }

    [Fact]
    public void ToInteropOptions_TlsDisabled_WhenNoApiKeyAndTlsNotSet()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        // TLS should be disabled when no API key and TLS not set
        Assert.True(interopOptions.tls_options == null);
    }

    [Fact]
    public void ToInteropOptions_TlsEnabled_WhenExplicitlySet()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
            Tls = new TlsOptions(),
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        // TLS should be enabled when explicitly set
        Assert.True(interopOptions.tls_options != null);
    }
}
