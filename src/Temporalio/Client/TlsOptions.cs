using System;

namespace Temporalio.Client
{
    /// <summary>
    /// TLS options for Temporal connections.
    /// </summary>
    public class TlsOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the PEM-formatted root CA certificate to validate the server certificate
        /// against.
        /// </summary>
        public byte[]? ServerRootCACert { get; set; }

        /// <summary>
        /// Gets or sets the TLS domain for SNI.
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Gets or sets the PEM-formatted client certificate for mTLS.
        /// </summary>
        /// <remarks>
        /// This must be combined with <see cref="ClientPrivateKey" />.
        /// </remarks>
        public byte[]? ClientCert { get; set; }

        /// <summary>
        /// Gets or sets the PEM-formatted client private key for mTLS.
        /// </summary>
        /// <remarks>
        /// This must be combined with <see cref="ClientCert" />.
        /// </remarks>
        public byte[]? ClientPrivateKey { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        /// <remarks>Does not create copies of byte arrays.</remarks>
        public virtual object Clone() => MemberwiseClone();
    }
}
