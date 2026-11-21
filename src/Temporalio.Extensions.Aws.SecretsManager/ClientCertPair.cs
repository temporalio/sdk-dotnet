namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// A client cert (chain) and key, as PEM.
    /// </summary>
    internal sealed class ClientCertPair
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientCertPair"/> class.
        /// </summary>
        /// <param name="cert">PEM cert chain.</param>
        /// <param name="key">PEM private key.</param>
        public ClientCertPair(byte[] cert, byte[] key)
        {
            Cert = cert;
            Key = key;
        }

        /// <summary>
        /// Gets the PEM cert chain.
        /// </summary>
        public byte[] Cert { get; }

        /// <summary>
        /// Gets the PEM private key.
        /// </summary>
        public byte[] Key { get; }
    }
}
