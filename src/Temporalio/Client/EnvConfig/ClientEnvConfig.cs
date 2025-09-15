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
        /// <param name="configSource">The data source to load from.</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>Loaded configuration data.</returns>
        public static ClientEnvConfig Load(
            DataSource? configSource = null,
            bool configFileStrict = false,
            IReadOnlyDictionary<string, string>? overrideEnvVars = null)
        {
            var runtime = Runtime.TemporalRuntime.Default.Runtime;
            var profiles = Bridge.EnvConfig.LoadClientConfig(
                runtime,
                configSource,
                configFileStrict,
                overrideEnvVars);
            return new ClientEnvConfig(profiles);
        }

        /// <summary>
        /// Load client connection options directly from configuration.
        /// </summary>
        /// <param name="profile">Name of the profile to load. If null, "default" is used.</param>
        /// <param name="configSource">The data source to load from.</param>
        /// <param name="disableFile">If true, do not load from file (only from environment).</param>
        /// <param name="disableEnv">If true, disable environment variable overrides.</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>Client connection options.</returns>
        public static TemporalClientConnectOptions LoadClientConnectOptions(
            string? profile = null,
            DataSource? configSource = null,
            bool disableFile = false,
            bool disableEnv = false,
            bool configFileStrict = false,
            IReadOnlyDictionary<string, string>? overrideEnvVars = null)
        {
            var profileName = profile ?? "default";
            var clientProfile = ConfigProfile.Load(
                profileName,
                configSource,
                disableFile,
                disableEnv,
                configFileStrict,
                overrideEnvVars);
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
        public sealed record ConfigProfile
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ConfigProfile"/> class.
            /// </summary>
            /// <param name="address">Client address.</param>
            /// <param name="namespace">Client namespace.</param>
            /// <param name="apiKey">Client API key.</param>
            /// <param name="tls">TLS configuration.</param>
            /// <param name="grpcMeta">gRPC metadata.</param>
            public ConfigProfile(
                string? address = null,
                string? @namespace = null,
                string? apiKey = null,
                Tls? tls = null,
                IReadOnlyDictionary<string, string>? grpcMeta = null)
            {
                Address = address;
                Namespace = @namespace;
                ApiKey = apiKey;
                Tls = tls;
                GrpcMeta = grpcMeta;
            }

            /// <summary>
            /// Gets the client address.
            /// </summary>
            public string? Address { get; private init; }

            /// <summary>
            /// Gets the client namespace.
            /// </summary>
            public string? Namespace { get; private init; }

            /// <summary>
            /// Gets the client API key.
            /// </summary>
            public string? ApiKey { get; private init; }

            /// <summary>
            /// Gets the TLS configuration.
            /// </summary>
            public Tls? Tls { get; private init; }

            /// <summary>
            /// Gets the gRPC metadata.
            /// </summary>
            public IReadOnlyDictionary<string, string>? GrpcMeta { get; private init; }

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
            public static ConfigProfile Load(
                string? profile = null,
                DataSource? configSource = null,
                bool disableFile = false,
                bool disableEnv = false,
                bool configFileStrict = false,
                IReadOnlyDictionary<string, string>? overrideEnvVars = null)
            {
                var runtime = Runtime.TemporalRuntime.Default.Runtime;
                return Bridge.EnvConfig.LoadClientConfigProfile(
                    runtime,
                    profile,
                    configSource,
                    disableFile,
                    disableEnv,
                    configFileStrict,
                    overrideEnvVars);
            }

            /// <summary>
            /// Create a Profile from a record structure.
            /// </summary>
            /// <param name="record">The record to convert from.</param>
            /// <returns>Profile configuration instance.</returns>
            public static ConfigProfile FromRecord(ProfileRecord record)
            {
                return new ConfigProfile(
                    address: record.Address,
                    @namespace: record.Namespace,
                    apiKey: record.ApiKey,
                    tls: record.Tls != null ? Tls.FromRecord(record.Tls) : null,
                    grpcMeta: record.GrpcMeta);
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

                    // If API key is present and TLS doesn't have a domain, extract it from address
                    if (ApiKey != null && string.IsNullOrEmpty(options.Tls.Domain) && Address != null)
                    {
                        var domain = Address.Split(':')[0];
                        options.Tls.Domain = domain;
                    }
                }
                else if (ApiKey != null && (Tls == null || Tls.Disabled != true) && Address != null)
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
        public sealed record Tls
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Tls"/> class.
            /// </summary>
            /// <param name="serverName">SNI override.</param>
            /// <param name="serverRootCaCert">Server CA certificate source.</param>
            /// <param name="clientCert">Client certificate source.</param>
            /// <param name="clientPrivateKey">Client key source.</param>
            public Tls(
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
            /// If null, TLS behavior depends on other factors (API key presence, etc.).
            /// If true, TLS is explicitly disabled. If false, TLS is explicitly enabled.
            /// </summary>
            public bool? Disabled { get; internal init; }

            /// <summary>
            /// Gets the SNI override.
            /// </summary>
            public string? ServerName { get; private init; }

            /// <summary>
            /// Gets the server CA certificate source.
            /// </summary>
            public DataSource? ServerRootCACert { get; private init; }

            /// <summary>
            /// Gets the client certificate source.
            /// </summary>
            public DataSource? ClientCert { get; private init; }

            /// <summary>
            /// Gets the client key source.
            /// </summary>
            public DataSource? ClientPrivateKey { get; private init; }

            /// <summary>
            /// Create a Tls from a record structure.
            /// </summary>
            /// <param name="record">The record to convert from.</param>
            /// <returns>TLS configuration instance.</returns>
            public static Tls FromRecord(TlsRecord record)
            {
                var tls = new Tls(
                    serverName: record.ServerName,
                    serverRootCaCert: DataSource.FromDictionary(record.ServerCaCert),
                    clientCert: DataSource.FromDictionary(record.ClientCert),
                    clientPrivateKey: DataSource.FromDictionary(record.ClientKey))
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
                    ServerCaCert: DataSource.ToDictionary(ServerRootCACert),
                    ClientCert: DataSource.ToDictionary(ClientCert),
                    ClientKey: DataSource.ToDictionary(ClientPrivateKey));
            }
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
        public static DataSource FromString(string content)
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
                return DataSource.FromString(dictionary["data"]);
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
        /// <param name="source">The data source to convert.</param>
        /// <returns>A dictionary with either "path" or "data" key, or null if source is null.</returns>
        public static Dictionary<string, string>? ToDictionary(DataSource? source)
        {
            if (source == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(source.Path))
            {
                return new Dictionary<string, string> { ["path"] = source.Path! };
            }

            if (source.Data != null)
            {
                return new Dictionary<string, string> { ["data"] = Encoding.UTF8.GetString(source.Data) };
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