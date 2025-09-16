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
    public sealed record ClientEnvConfig(IReadOnlyDictionary<string, ClientEnvConfig.ConfigProfile> Profiles)
    {
        /// <summary>
        /// Load client configuration from environment variables and configuration files.
        /// </summary>
        /// <param name="options">Options for loading the configuration.</param>
        /// <returns>Loaded configuration data.</returns>
        public static ClientEnvConfig Load(ConfigLoadOptions options)
        {
            var runtime = Runtime.TemporalRuntime.Default.Runtime;
            var profiles = Bridge.EnvConfig.LoadClientConfig(runtime, options);
            return new ClientEnvConfig(profiles);
        }

        /// <summary>
        /// Load client connection options directly from configuration.
        /// </summary>
        /// <param name="options">Options for loading the configuration profile.</param>
        /// <returns>Client connection options.</returns>
        public static TemporalClientConnectOptions LoadClientConnectOptions(ProfileLoadOptions options)
        {
            var clientProfile = ConfigProfile.Load(options);
            return clientProfile.ToClientConnectionOptions();
        }

        /// <summary>
        /// Convert to a record structure that can be used for TOML serialization.
        /// </summary>
        /// <returns>Dictionary mapping profile names to their record representations.</returns>
        public IReadOnlyDictionary<string, ProfileRecord> ToRecord()
        {
            return Profiles.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToRecord());
        }

        /// <summary>
        /// Create a ClientEnvConfig from a record structure.
        /// </summary>
        /// <param name="profileRecords">Dictionary of profile name to profile record.</param>
        /// <returns>Client configuration instance.</returns>
        public static ClientEnvConfig FromRecord(IReadOnlyDictionary<string, ProfileRecord> profileRecords)
        {
            var profiles = profileRecords.ToDictionary(
                kvp => kvp.Key,
                kvp => ConfigProfile.FromRecord(kvp.Value));

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
        public sealed record ConfigProfile(
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
            public static ConfigProfile Load(ProfileLoadOptions options)
            {
                var runtime = Runtime.TemporalRuntime.Default.Runtime;
                return Bridge.EnvConfig.LoadClientConfigProfile(runtime, options);
            }

            /// <summary>
            /// Create a Profile from a record structure.
            /// </summary>
            /// <param name="record">The record to convert from.</param>
            /// <returns>Profile configuration instance.</returns>
            public static ConfigProfile FromRecord(ProfileRecord record)
            {
                return new ConfigProfile(
                    Address: record.Address,
                    Namespace: record.Namespace,
                    ApiKey: record.ApiKey,
                    Tls: record.Tls != null ? Tls.FromRecord(record.Tls) : null,
                    GrpcMeta: record.GrpcMeta);
            }

            /// <summary>
            /// Create a <see cref="TemporalClientConnectOptions"/> from this profile.
            /// </summary>
            /// <returns>Connection options for a client.</returns>
            public TemporalClientConnectOptions ToClientConnectionOptions()
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
                if (Tls != null && Tls.Disabled != true)
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
            /// Convert to a record structure that can be used for TOML serialization.
            /// </summary>
            /// <returns>Record representation of this profile.</returns>
            public ProfileRecord ToRecord()
            {
                return new ProfileRecord(
                    Address: Address,
                    Namespace: Namespace,
                    ApiKey: ApiKey,
                    Tls: Tls?.ToRecord(),
                    GrpcMeta: GrpcMeta);
            }
        }

        /// <summary>
        /// TLS configuration as specified as part of client configuration.
        /// </summary>
        /// <param name="ServerName">SNI override.</param>
        /// <param name="ServerRootCACert">Server CA certificate source.</param>
        /// <param name="ClientCert">Client certificate source.</param>
        /// <param name="ClientPrivateKey">Client key source.</param>
        public sealed record Tls(
            string? ServerName = null,
            DataSource? ServerRootCACert = null,
            DataSource? ClientCert = null,
            DataSource? ClientPrivateKey = null)
        {
            /// <summary>
            /// Gets a value indicating whether TLS is explicitly disabled.
            /// If null, TLS behavior depends on other factors (API key presence, etc.).
            /// If true, TLS is explicitly disabled. If false, TLS is explicitly enabled.
            /// </summary>
            public bool? Disabled { get; internal init; }

            /// <summary>
            /// Create a Tls from a record structure.
            /// </summary>
            /// <param name="record">The record to convert from.</param>
            /// <returns>TLS configuration instance.</returns>
            public static Tls FromRecord(TlsRecord record)
            {
                var tls = new Tls(
                    ServerName: record.ServerName,
                    ServerRootCACert: DataSource.FromDictionary(record.ServerCaCert),
                    ClientCert: DataSource.FromDictionary(record.ClientCert),
                    ClientPrivateKey: DataSource.FromDictionary(record.ClientKey))
                {
                    Disabled = record.Disabled,
                };

                return tls;
            }

            /// <summary>
            /// Create a <see cref="TlsOptions"/> from this configuration.
            /// </summary>
            /// <returns>TLS options for a client.</returns>
            public TlsOptions ToTlsOptions()
            {
                if (Disabled == true)
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
            /// Convert to a record structure that can be used for TOML serialization.
            /// </summary>
            /// <returns>Record representation of this TLS config.</returns>
            public TlsRecord ToRecord()
            {
                return new TlsRecord(
                    Disabled: Disabled,
                    ServerName: ServerName,
                    ServerCaCert: ServerRootCACert?.ToDictionary(),
                    ClientCert: ClientCert?.ToDictionary(),
                    ClientKey: ClientPrivateKey?.ToDictionary());
            }
        }

        /// <summary>
        /// Options for loading a specific client configuration profile.
        /// </summary>
        public class ProfileLoadOptions : ICloneable
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ProfileLoadOptions"/> class.
            /// </summary>
            public ProfileLoadOptions()
            {
            }

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
            /// Initializes a new instance of the <see cref="ConfigLoadOptions"/> class.
            /// </summary>
            public ConfigLoadOptions()
            {
            }

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
            if (!string.IsNullOrEmpty(this.Path))
            {
                return new Dictionary<string, string> { ["path"] = this.Path! };
            }

            if (this.Data != null)
            {
                return new Dictionary<string, string> { ["data"] = Encoding.UTF8.GetString(this.Data) };
            }

            return null;
        }
    }

    /// <summary>
    /// Record representation of TLS config for TOML serialization.
    /// </summary>
    public sealed record TlsRecord(
        bool? Disabled = null,
        string? ServerName = null,
        IReadOnlyDictionary<string, string>? ServerCaCert = null,
        IReadOnlyDictionary<string, string>? ClientCert = null,
        IReadOnlyDictionary<string, string>? ClientKey = null);

    /// <summary>
    /// Record representation of a client config profile for TOML serialization.
    /// </summary>
    public sealed record ProfileRecord(
        string? Address = null,
        string? Namespace = null,
        string? ApiKey = null,
        TlsRecord? Tls = null,
        IReadOnlyDictionary<string, string>? GrpcMeta = null);
}