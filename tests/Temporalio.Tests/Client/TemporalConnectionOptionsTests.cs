namespace Temporalio.Tests.Client;

using System.Text;
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

    [Fact]
    public void ToInteropOptions_Metadata_NotSet()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        Assert.True(interopOptions.metadata.data == null);
        Assert.Equal((nuint)0, interopOptions.metadata.size);
    }

    [Fact]
    public void ToInteropOptions_Metadata_Empty()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
            RpcMetadata = new Dictionary<string, string>(),
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        Assert.True(interopOptions.metadata.data == null);
        Assert.Equal((nuint)0, interopOptions.metadata.size);
    }

    [Fact]
    public void ToInteropOptions_Metadata_Some()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
            RpcMetadata = new Dictionary<string, string>
            {
                { "key1", "client-ascii" },
                { "x-key1", "client-ascii-more" },
            },
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        Assert.Equal((nuint)2, interopOptions.metadata.size);

        Bridge.Interop.TemporalCoreByteArrayRef* data = interopOptions.metadata.data;
        AssertKeyValue(data[0], "key1", "client-ascii");
        AssertKeyValue(data[1], "x-key1", "client-ascii-more");
    }

    [Fact]
    public void ToInteropOptions_RpcBinaryMetadata_NotSet()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        Assert.True(interopOptions.binary_metadata.data == null);
        Assert.Equal((nuint)0, interopOptions.binary_metadata.size);
    }

    [Fact]
    public void ToInteropOptions_RpcBinaryMetadata_Empty()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
            RpcBinaryMetadata = new Dictionary<string, byte[]>(),
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        Assert.True(interopOptions.binary_metadata.data == null);
        Assert.Equal((nuint)0, interopOptions.binary_metadata.size);
    }

    [Fact]
    public void ToInteropOptions_RpcBinaryMetadata_Some()
    {
        var options = new TemporalConnectionOptions("localhost:7233")
        {
            Identity = "test-identity",
            RpcBinaryMetadata = new Dictionary<string, byte[]>
            {
                { "key1-bin", Encoding.UTF8.GetBytes("client-binary") },
                { "x-key1-bin", Encoding.UTF8.GetBytes("client-binary-more") },
            },
        };

        using var scope = new Scope();
        var interopOptions = options.ToInteropOptions(scope);

        Assert.Equal((nuint)2, interopOptions.binary_metadata.size);

        Bridge.Interop.TemporalCoreByteArrayRef* data = interopOptions.binary_metadata.data;
        AssertKeyValue(data[0], "key1-bin", "client-binary");
        AssertKeyValue(data[1], "x-key1-bin", "client-binary-more");
    }

    private static void AssertKeyValue(Bridge.Interop.TemporalCoreByteArrayRef byteArrayRef, string expectedKey, string expectedValue)
    {
        byte[] expectedKeyBytes = Encoding.UTF8.GetBytes(expectedKey);
        byte[] expectedValueBytes = Encoding.UTF8.GetBytes(expectedValue);
        ReadOnlySpan<byte> byteArrayRefSpan = new(byteArrayRef.data, (int)byteArrayRef.size);

        // Expect format of "<key>\n<value>" in bytes
        Assert.Equal(expectedKeyBytes.Length + expectedValueBytes.Length + 1, byteArrayRefSpan.Length);
        Assert.Equal(expectedKeyBytes, byteArrayRefSpan.Slice(0, expectedKeyBytes.Length).ToArray());
        Assert.Equal(0x0A, byteArrayRefSpan[expectedKeyBytes.Length]); // newline
        Assert.Equal(expectedValueBytes, byteArrayRefSpan.Slice(expectedKeyBytes.Length + 1).ToArray());
    }
}
