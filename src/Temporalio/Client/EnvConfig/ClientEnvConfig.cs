using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Temporalio.Client.EnvConfig
{
    /// <summary>
    /// Represents the overall client configuration.
    /// </summary>
    /// <param name="Profiles">The configuration profiles.</param>
    public sealed record ClientEnvConfig(IReadOnlyDictionary<string, ClientEnvConfig.Profile> Profiles)
    {
        /// <summary>
        /// Load client configuration from environment variables and configuration files.
        /// </summary>
        /// <param name="options">Options for loading the configuration.</param>
        /// <returns>Loaded configuration data.</returns>
        public static ClientEnvConfig Load(ConfigLoadOptions? options = null)
        {
            var runtime = Runtime.TemporalRuntime.Default.Runtime;
            var profiles = Bridge.EnvConfig.LoadClientConfig(runtime, options ?? new ConfigLoadOptions());
            return new ClientEnvConfig(profiles);
        }

        /// <summary>
        /// Load client connection options directly from configuration.
        /// </summary>
        /// <param name="options">Options for loading the configuration profile.</param>
        /// <returns>Client connection options.</returns>
        public static TemporalClientConnectOptions LoadClientConnectOptions(ProfileLoadOptions? options = null)
        {
            var clientProfile = Profile.Load(options);
            return clientProfile.ToClientConnectionOptions();
        }

        /// <summary>
        /// Convert to a dictionary structure that can be used for TOML serialization.
        /// </summary>
        /// <returns>Dictionary mapping profile names to their dictionary representations.</returns>
        public IReadOnlyDictionary<string, Dictionary<string, object>> ToDictionary() =>
            Profiles.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToDictionary());

        /// <summary>
        /// Create a ClientEnvConfig from a dictionary structure.
        /// </summary>
        /// <param name="profileDictionaries">Dictionary of profile name to profile dictionary.</param>
        /// <returns>Client configuration instance.</returns>
        public static ClientEnvConfig FromDictionary(IReadOnlyDictionary<string, Dictionary<string, object>> profileDictionaries)
        {
            var profiles = profileDictionaries.ToDictionary(
                kvp => kvp.Key,
                kvp => Profile.FromDictionary(kvp.Value));

            return new ClientEnvConfig(profiles);
        }

        /// <summary>
        /// Represents a client configuration profile.
        /// </summary>
        /// <param name="Address">Client address.</param>
        /// <param name="Namespace">Client namespace.</param>
        /// <param name="ApiKey">Client API key.</param>
        /// <param name="Tls">TLS configuration.</param>
        /// <param name="GrpcMeta">gRPC metadata.</param>
        #pragma warning disable CA1724 // We're ok with the Profile name since it's a nested class
        public sealed record Profile(
        #pragma warning restore CA1724
            string? Address = null,
            string? Namespace = null,
            string? ApiKey = null,
            Tls? Tls = null,
            IReadOnlyDictionary<string, string>? GrpcMeta = null)
        {
            /// <summary>
            /// Loads a specific profile with environment variable overrides.
            /// </summary>
            /// <param name="options">Options for loading the configuration profile.</param>
            /// <returns>The loaded profile.</returns>
            public static Profile Load(ProfileLoadOptions? options = null)
            {
                var runtime = Runtime.TemporalRuntime.Default.Runtime;
                return Bridge.EnvConfig.LoadClientConfigProfile(runtime, options ?? new ProfileLoadOptions());
            }

            /// <summary>
            /// Create a Profile from a dictionary structure.
            /// </summary>
            /// <param name="dictionary">The dictionary to convert from.</param>
            /// <returns>Profile configuration instance.</returns>
            public static Profile FromDictionary(IReadOnlyDictionary<string, object> dictionary) =>
                new(
                    Address: dictionary.TryGetValue("address", out var address) ? (string?)address : null,
                    Namespace: dictionary.TryGetValue("namespace", out var nameSpace) ? (string?)nameSpace : null,
                    ApiKey: dictionary.TryGetValue("api_key", out var apiKey) ? (string?)apiKey : null,
                    Tls: dictionary.TryGetValue("tls", out var tls) && tls is IReadOnlyDictionary<string, object> tlsDict ? Tls.FromDictionary(tlsDict) : null,
                    GrpcMeta: dictionary.TryGetValue("grpc_meta", out var grpcMeta) ? (IReadOnlyDictionary<string, string>?)grpcMeta : null);

            /// <summary>
            /// Create a <see cref="TemporalClientConnectOptions"/> from this profile.
            /// </summary>
            /// <returns>Connection options for a client.</returns>
            public TemporalClientConnectOptions ToClientConnectionOptions()
            {
                var options = new TemporalClientConnectOptions(Address ?? string.Empty);
                // Set namespace if provided
                if (Namespace != null)
                {
                    options.Namespace = Namespace;
                }

                // Set default TLS options if API key exists
                if (ApiKey != null)
                {
                    options.ApiKey = ApiKey;
                    options.Tls = new TlsOptions();
                }

                // Override with explicit TLS configuration if provided
                if (Tls != null)
                {
                    options.Tls = Tls.ToTlsOptions();
                }

                // Add gRPC metadata if present
                if (GrpcMeta != null)
                {
                    options.RpcMetadata = new List<KeyValuePair<string, string>>(GrpcMeta);
                }

                return options;
            }

            /// <summary>
            /// Convert to a dictionary structure that can be used for TOML serialization.
            /// </summary>
            /// <returns>Dictionary representation of this profile.</returns>
            public Dictionary<string, object> ToDictionary()
            {
                var result = new Dictionary<string, object>();

                if (Address != null)
                {
                    result["address"] = Address;
                }

                if (Namespace != null)
                {
                    result["namespace"] = Namespace;
                }

                if (ApiKey != null)
                {
                    result["api_key"] = ApiKey;
                }

                var tlsDict = Tls?.ToDictionary();
                if (tlsDict != null)
                {
                    result["tls"] = tlsDict;
                }

                if (GrpcMeta != null)
                {
                    result["grpc_meta"] = GrpcMeta;
                }

                return result;
            }
        }

        /// <summary>
        /// TLS configuration as specified as part of client configuration.
        /// </summary>
        /// <param name="Disabled">Flag that determines if TLS is enabled. If null, TLS behavior depends on other factors (API key presence, etc.)</param>
        /// <param name="ServerName">SNI override.</param>
        /// <param name="ServerRootCACert">Server CA certificate source.</param>
        /// <param name="ClientCert">Client certificate source.</param>
        /// <param name="ClientPrivateKey">Client key source.</param>
        public sealed record Tls(
            bool? Disabled = null,
            string? ServerName = null,
            DataSource? ServerRootCACert = null,
            DataSource? ClientCert = null,
            DataSource? ClientPrivateKey = null)
        {
            /// <summary>
            /// Create a Tls from a dictionary structure.
            /// </summary>
            /// <param name="dictionary">The dictionary to convert from.</param>
            /// <returns>TLS configuration instance.</returns>
            public static Tls FromDictionary(IReadOnlyDictionary<string, object> dictionary) => new(
                    ServerName: dictionary.TryGetValue("server_name", out var serverName) ? (string?)serverName : null,
                    ServerRootCACert: dictionary.TryGetValue("server_ca_cert", out var serverCaCert) ? DataSource.FromDictionary((IReadOnlyDictionary<string, string>?)serverCaCert) : null,
                    ClientCert: dictionary.TryGetValue("client_cert", out var clientCert) ? DataSource.FromDictionary((IReadOnlyDictionary<string, string>?)clientCert) : null,
                    ClientPrivateKey: dictionary.TryGetValue("client_key", out var clientKey) ? DataSource.FromDictionary((IReadOnlyDictionary<string, string>?)clientKey) : null,
                    Disabled: dictionary.TryGetValue("disabled", out var disabled) ? (bool?)disabled : null);

            /// <summary>
            /// Create a <see cref="TlsOptions"/> from this configuration.
            /// </summary>
            /// <returns>TLS options for a client.</returns>
            public TlsOptions? ToTlsOptions()
            {
                if (Disabled == true)
                {
                    return null;
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
            /// Convert to a dictionary structure that can be used for TOML serialization.
            /// </summary>
            /// <returns>Dictionary representation of this TLS config.</returns>
            public Dictionary<string, object> ToDictionary()
            {
                var result = new Dictionary<string, object>();

                if (Disabled != null)
                {
                    result["disabled"] = Disabled;
                }

                if (ServerName != null)
                {
                    result["server_name"] = ServerName;
                }

                var serverCaCertDict = ServerRootCACert?.ToDictionary();
                if (serverCaCertDict != null)
                {
                    result["server_ca_cert"] = serverCaCertDict;
                }

                var clientCertDict = ClientCert?.ToDictionary();
                if (clientCertDict != null)
                {
                    result["client_cert"] = clientCertDict;
                }

                var clientKeyDict = ClientPrivateKey?.ToDictionary();
                if (clientKeyDict != null)
                {
                    result["client_key"] = clientKeyDict;
                }

                return result;
            }
        }

        /// <summary>
        /// Options for loading a specific client configuration profile.
        /// </summary>
        public class ProfileLoadOptions : ICloneable
        {
            /// <summary>
            /// Gets or sets the name of the profile to load. If null, "default" is used.
            /// </summary>
            public string? Profile { get; set; }

            /// <summary>
            /// Gets or sets the data source to load configuration from. If null, the configuration
            /// will be loaded from the default file path: os-specific-config-dir/temporalio/temporal.toml.
            /// </summary>
            public DataSource? ConfigSource { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to disable loading from file (only from environment).
            /// Default is false.
            /// </summary>
            public bool DisableFile { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to disable environment variable overrides.
            /// Default is false.
            /// </summary>
            public bool DisableEnv { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to fail if configuration file is invalid.
            /// Default is false.
            /// </summary>
            public bool ConfigFileStrict { get; set; }

            /// <summary>
            /// Gets or sets environment variables to use, or null to use system environment.
            /// </summary>
            public IReadOnlyDictionary<string, string>? OverrideEnvVars { get; set; }

            /// <summary>
            /// Create a shallow copy of these options.
            /// </summary>
            /// <returns>A shallow copy of these options.</returns>
            public virtual object Clone() => MemberwiseClone();
        }

        /// <summary>
        /// Options for loading client environment configuration.
        /// </summary>
        public class ConfigLoadOptions : ICloneable
        {
            /// <summary>
            /// Gets or sets the data source to load configuration from. If null, the configuration
            /// will be loaded from the default file path: os-specific-config-dir/temporalio/temporal.toml.
            /// </summary>
            public DataSource? ConfigSource { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether to fail if configuration file is invalid.
            /// Default is false.
            /// </summary>
            public bool ConfigFileStrict { get; set; }

            /// <summary>
            /// Gets or sets environment variables to use, or null to use system environment.
            /// </summary>
            public IReadOnlyDictionary<string, string>? OverrideEnvVars { get; set; }

            /// <summary>
            /// Create a shallow copy of these options.
            /// </summary>
            /// <returns>A shallow copy of these options.</returns>
            public virtual object Clone() => MemberwiseClone();
        }
    }

    /// <summary>
    /// A data source for configuration, which can be a path to a file,
    /// the string contents of a file, or raw bytes.
    /// </summary>
    public sealed class DataSource
    {
        private DataSource()
        {
        }

        /// <summary>
        /// Gets the file path for this data source, if applicable.
        /// </summary>
        public string? Path { get; private init; }

        /// <summary>
        /// Gets the raw data for this data source, if applicable.
        /// </summary>
        public byte[]? Data { get; private init; }

        /// <summary>
        /// Create a data source from a file path.
        /// </summary>
        /// <param name="path">Path to the configuration file.</param>
        /// <returns>A new data source for the specified file.</returns>
        /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
        public static DataSource FromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

            return new DataSource { Path = path };
        }

        /// <summary>
        /// Create a data source from string content.
        /// </summary>
        /// <param name="content">Configuration data as a UTF-8 string.</param>
        /// <returns>A new data source for the specified data.</returns>
        /// <exception cref="ArgumentException">Thrown when content is null.</exception>
        public static DataSource FromUTF8String(string content)
        {
            if (content == null)
            {
                throw new ArgumentException("Content cannot be null", nameof(content));
            }

            return new DataSource { Data = Encoding.UTF8.GetBytes(content) };
        }

        /// <summary>
        /// Create a data source from raw bytes.
        /// </summary>
        /// <param name="data">Configuration data as raw bytes.</param>
        /// <returns>A new data source for the specified data.</returns>
        /// <exception cref="ArgumentException">Thrown when data is null or empty.</exception>
        public static DataSource FromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }

            return new DataSource { Data = data };
        }

        /// <summary>
        /// Create a data source from a dictionary containing either "data" or "path" keys.
        /// </summary>
        /// <param name="dictionary">Dictionary containing configuration data or path.</param>
        /// <returns>A new data source, or null if the dictionary is null or contains neither "data" nor "path".</returns>
        public static DataSource? FromDictionary(IReadOnlyDictionary<string, string>? dictionary)
        {
            if (dictionary == null)
            {
                return null;
            }

            if (dictionary.ContainsKey("data"))
            {
                return DataSource.FromUTF8String(dictionary["data"]);
            }

            if (dictionary.ContainsKey("path"))
            {
                return DataSource.FromPath(dictionary["path"]);
            }

            return null;
        }

        /// <summary>
        /// Convert a data source to a dictionary for TOML serialization.
        /// </summary>
        /// <returns>A dictionary with either "path" or "data" key, or null if neither key exists.</returns>
        public Dictionary<string, string>? ToDictionary()
        {
            if (!string.IsNullOrEmpty(Path))
            {
                return new Dictionary<string, string> { ["path"] = Path! };
            }

            if (Data != null)
            {
                return new Dictionary<string, string> { ["data"] = Encoding.UTF8.GetString(Data) };
            }

            return null;
        }
    }
}