namespace Temporalio.Client
{
    /// <summary>
    /// Options for <see cref="TemporalCloudOperationsClient.ConnectAsync" />. Unlike
    /// <see cref="TemporalClient.ConnectAsync"/>, this defaults <c>TargetHost</c> to the known host
    /// and <c>Tls</c> to enabled.
    /// </summary>
    /// <remarks>
    /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
    /// </remarks>
    public class TemporalCloudOperationsClientConnectOptions : TemporalConnectionOptions
    {
        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="TemporalCloudOperationsClientConnectOptions"/> class. Most users will at
        /// least need an API key and therefore should use the other constructor overload.
        /// </summary>
        /// <remarks>
        /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
        /// </remarks>
        public TemporalCloudOperationsClientConnectOptions()
        {
            // Default some values we wouldn't default for the regular connection
            TargetHost = "saas-api.tmprl.cloud:443";
            Tls = new();
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="TemporalCloudOperationsClientConnectOptions"/> class.
        /// </summary>
        /// <param name="apiKey">API key for the client.</param>
        /// <remarks>
        /// WARNING: Cloud Operations API and its client are experimental and APIs may change.
        /// </remarks>
        public TemporalCloudOperationsClientConnectOptions(string apiKey)
            : this()
        {
            ApiKey = apiKey;
        }

        /// <summary>
        /// Gets or sets the version header for safer mutation. Note, upon connect, this is actually
        /// merged into the RPC metadata as the <c>temporal-cloud-api-version</c> header and users
        /// must retain that header if needed if/when RPC metadata is updated.
        /// </summary>
        public string? Version { get; set; }
    }
}