namespace Temporalio.Client
{
    public class TlsOptions
    {
        public byte[]? ServerRootCACert { get; set; }

        public string? Domain { get; set; }

        public byte[]? ClientCert { get; set; }

        public byte[]? ClientPrivateKey { get; set; }
    }
}
