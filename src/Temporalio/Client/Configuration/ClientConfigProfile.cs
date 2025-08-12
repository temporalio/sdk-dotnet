using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Temporalio.Client.Configuration
{
    /// <summary>
    /// Represents a client configuration profile.
    /// </summary>
    public sealed class ClientConfigProfile : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConfigProfile"/> class.
        /// </summary>
        /// <param name="address">Client address.</param>
        /// <param name="namespace">Client namespace.</param>
        /// <param name="apiKey">Client API key.</param>
        /// <param name="tls">TLS configuration.</param>
        /// <param name="grpcMeta">gRPC metadata.</param>
        public ClientConfigProfile(
            string? address = null,
            string? @namespace = null,
            string? apiKey = null,
            ClientConfigTls? tls = null,
            IDictionary<string, string>? grpcMeta = null)
        {
            Address = address;
            Namespace = @namespace;
            ApiKey = apiKey;
            Tls = tls;
            if (grpcMeta != null)
            {
                GrpcMeta = new Dictionary<string, string>(grpcMeta);
            }
        }

        /// <summary>
        /// Gets the client address.
        /// </summary>
        public string? Address { get; private set; }

        /// <summary>
        /// Gets the client namespace.
        /// </summary>
        public string? Namespace { get; private set; }

        /// <summary>
        /// Gets the client API key.
        /// </summary>
        public string? ApiKey { get; private set; }

        /// <summary>
        /// Gets the TLS configuration.
        /// </summary>
        public ClientConfigTls? Tls { get; private set; }

        /// <summary>
        /// Gets the gRPC metadata.
        /// </summary>
        public IDictionary<string, string>? GrpcMeta { get; private set; }

        /// <summary>
        /// Loads a specific profile with environment variable overrides.
        /// </summary>
        /// <param name="profile">Name of the profile to load. Defaults to "default" if not specified.</param>
        /// <param name="configSource">The data source to load from.</param>
        /// <param name="disableFile">If true, do not load from file (only from environment).</param>
        /// <param name="disableEnv">If true, disable environment variable overrides.</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>The loaded profile.</returns>
        public static ClientConfigProfile Load(
            string? profile = null,
            DataSource? configSource = null,
            bool disableFile = false,
            bool disableEnv = false,
            bool configFileStrict = false,
            Dictionary<string, string>? overrideEnvVars = null)
        {
            if (disableFile && disableEnv)
            {
                throw new InvalidOperationException("Cannot disable both file and environment sources - at least one must be enabled");
            }

            var runtime = Temporalio.Runtime.TemporalRuntime.Default.Runtime;
            return Bridge.EnvConfig.LoadClientConfigProfile(
                runtime,
                profile ?? "default",
                configSource ?? DataSource.FromDefault(),
                disableFile,
                disableEnv,
                configFileStrict,
                overrideEnvVars);
        }

        /// <summary>
        /// Deserialize profile configuration from JSON.
        /// </summary>
        /// <param name="json">JSON string to deserialize.</param>
        /// <param name="options">Optional JSON serializer options.</param>
        /// <returns>Profile configuration instance.</returns>
        public static ClientConfigProfile FromJson(string json, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Deserialize<ClientConfigProfile>(json, options ?? GetDefaultJsonOptions())
                ?? throw new JsonException("Failed to deserialize ClientConfigProfile from JSON.");
        }

        /// <summary>
        /// Create a <see cref="TemporalClientConnectOptions"/> from this profile.
        /// </summary>
        /// <returns>Connection options for a client.</returns>
        public TemporalClientConnectOptions ToConnectionOptions()
        {
            var options = new TemporalClientConnectOptions(Address ?? string.Empty)
            {
                ApiKey = ApiKey,
            };

            // Set namespace if provided
            if (Namespace != null)
            {
                options.Namespace = Namespace;
            }

            // Configure TLS options
            if (Tls != null && !Tls.Disabled)
            {
                options.Tls = Tls.ToTlsOptions();

                // If API key is present and TLS doesn't have a domain, extract it from address
                if (ApiKey != null && string.IsNullOrEmpty(options.Tls.Domain) && Address != null)
                {
                    var domain = Address.Split(':')[0];
                    options.Tls.Domain = domain;
                }
            }
            else if (ApiKey != null && (Tls == null || !Tls.Disabled) && Address != null)
            {
                // If API key is provided but no TLS config (or TLS is not disabled), set up default TLS with domain from address
                var domain = Address.Split(':')[0];
                options.Tls = new TlsOptions { Domain = domain };
            }

            // Add gRPC metadata if present
            if (GrpcMeta != null)
            {
                options.RpcMetadata = new List<KeyValuePair<string, string>>(GrpcMeta);
            }

            return options;
        }

        /// <summary>
        /// Serialize this profile configuration to JSON.
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
            var newProfile = (ClientConfigProfile)MemberwiseClone();
            if (Tls != null)
            {
                newProfile.Tls = (ClientConfigTls)Tls.Clone();
            }
            if (GrpcMeta != null)
            {
                newProfile.GrpcMeta = new Dictionary<string, string>(GrpcMeta);
            }
            return newProfile;
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