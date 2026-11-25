namespace Temporalio.Tests.Extensions.Aws.SecretsManager;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

internal static class TestHelpers
{
    static TestHelpers()
    {
        (TestCertA, TestKeyA, TestKeyAEc) = NewCertKey("a");
        (TestCertB, TestKeyB, _) = NewCertKey("b");
    }

    public static string TestCertA { get; }

    public static string TestKeyA { get; }

    /// <summary>
    /// Gets <see cref="TestKeyA" /> in the traditional EC format.
    /// </summary>
    public static string TestKeyAEc { get; }

    public static string TestCertB { get; }

    public static string TestKeyB { get; }

    public static async Task WaitUntilAsync(Func<bool> cond)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!cond())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("condition not met within 5s");
            }
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Create a self-signed EC cert and its key, as PEM with a trailing newline. The cert is
    /// already expired, since it's only parsed and matched against its key, never used for TLS.
    /// </summary>
    private static (string Cert, string Key, string EcKey) NewCertKey(string name)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            $"CN=aws-secrets-manager-test-{name}", key, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var cert = request.CreateSelfSigned(now.AddDays(-2), now.AddDays(-1));
        return (
            cert.ExportCertificatePem() + "\n",
            key.ExportPkcs8PrivateKeyPem() + "\n",
            key.ExportECPrivateKeyPem() + "\n");
    }
}
