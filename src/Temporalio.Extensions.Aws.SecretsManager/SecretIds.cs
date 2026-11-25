namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Configured secret IDs. Empty strings are unset.
    /// </summary>
    internal sealed class SecretIds
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SecretIds"/> class.
        /// </summary>
        /// <param name="apiKey">API key secret ID.</param>
        /// <param name="clientCert">TLS client cert secret ID.</param>
        /// <param name="clientKey">TLS client key secret ID.</param>
        /// <param name="serverCACert">TLS server CA cert secret ID.</param>
        public SecretIds(
            string apiKey = "", string clientCert = "", string clientKey = "", string serverCACert = "")
        {
            ApiKey = apiKey;
            ClientCert = clientCert;
            ClientKey = clientKey;
            ServerCACert = serverCACert;
        }

        /// <summary>
        /// Gets the API key secret ID.
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// Gets the TLS client cert secret ID.
        /// </summary>
        public string ClientCert { get; }

        /// <summary>
        /// Gets the TLS client key secret ID.
        /// </summary>
        public string ClientKey { get; }

        /// <summary>
        /// Gets the TLS server CA cert secret ID.
        /// </summary>
        public string ServerCACert { get; }

        /// <summary>
        /// Gets a value indicating whether any TLS secret ID is set.
        /// </summary>
        public bool HasTls => ClientCert.Length > 0 || ClientKey.Length > 0 || ServerCACert.Length > 0;

        /// <summary>
        /// Gets a value indicating whether no secret ID is set.
        /// </summary>
        public bool IsEmpty => ApiKey.Length == 0 && !HasTls;
    }
}
