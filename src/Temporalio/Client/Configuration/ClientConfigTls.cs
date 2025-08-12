using System;
using System.Text.Json;

namespace Temporalio.Client.Configuration
{
    /// <summary>
    /// TLS configuration as specified as part of client configuration.
    /// </summary>
    public sealed class ClientConfigTls : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConfigTls"/> class.
        /// </summary>
        /// <param name="serverName">SNI override.</param>
        /// <param name="serverRootCaCert">Server CA certificate source.</param>
        /// <param name="clientCert">Client certificate source.</param>
        /// <param name="clientPrivateKey">Client key source.</param>
        public ClientConfigTls(
            string? serverName = null,
            DataSource? serverRootCaCert = null,
            DataSource? clientCert = null,
            DataSource? clientPrivateKey = null)
        {
            ServerName = serverName;
            ServerRootCACert = serverRootCaCert;
            ClientCert = clientCert;
            ClientPrivateKey = clientPrivateKey;
        }

        /// <summary>
        /// Gets a value indicating whether TLS is explicitly disabled.
        /// </summary>
        public bool Disabled { get; internal set; }

        /// <summary>
        /// Gets the SNI override.
        /// </summary>
        public string? ServerName { get; private set; }

        /// <summary>
        /// Gets the server CA certificate source.
        /// </summary>
        public DataSource? ServerRootCACert { get; private set; }

        /// <summary>
        /// Gets the client certificate source.
        /// </summary>
        public DataSource? ClientCert { get; private set; }

        /// <summary>
        /// Gets the client key source.
        /// </summary>
        public DataSource? ClientPrivateKey { get; private set; }

        /// <summary>
        /// Deserialize TLS configuration from JSON.
        /// </summary>
        /// <param name="json">JSON string to deserialize.</param>
        /// <param name="options">Optional JSON serializer options.</param>
        /// <returns>TLS configuration instance.</returns>
        public static ClientConfigTls FromJson(string json, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Deserialize<ClientConfigTls>(json, options ?? GetDefaultJsonOptions())
                ?? throw new JsonException("Failed to deserialize ClientConfigTls from JSON.");
        }

        /// <summary>
        /// Create a <see cref="TlsOptions"/> from this configuration.
        /// </summary>
        /// <returns>TLS options for a client.</returns>
        public TlsOptions ToTlsOptions()
        {
            if (Disabled)
            {
                return new TlsOptions();
            }

            return new TlsOptions
            {
                Domain = ServerName,
                ServerRootCACert = ServerRootCACert?.Data,
                ClientCert = ClientCert?.Data,
                ClientPrivateKey = ClientPrivateKey?.Data,
            };
        }

        /// <summary>
        /// Serialize this TLS configuration to JSON.
        /// </summary>
        /// <param name="options">Optional JSON serializer options.</param>
        /// <returns>JSON string representation.</returns>
        public string ToJson(JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Serialize(this, options ?? GetDefaultJsonOptions());
        }

        /// <inheritdoc />
        public object Clone()
        {
            return MemberwiseClone();
        }

        private static JsonSerializerOptions GetDefaultJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            };
        }
    }
}