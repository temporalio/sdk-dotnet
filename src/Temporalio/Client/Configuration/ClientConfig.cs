using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Temporalio.Client.Configuration
{
    /// <summary>
    /// Represents the overall client configuration.
    /// </summary>
    public sealed class ClientConfig : ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConfig"/> class.
        /// </summary>
        /// <param name="profiles">The configuration profiles.</param>
        public ClientConfig(IReadOnlyDictionary<string, ClientConfigProfile> profiles)
        {
            Profiles = profiles ?? new Dictionary<string, ClientConfigProfile>();
        }

        /// <summary>
        /// Gets the configuration profiles.
        /// </summary>
        public IReadOnlyDictionary<string, ClientConfigProfile> Profiles { get; private set; }

        /// <summary>
        /// Load client configuration from environment variables and configuration files.
        /// </summary>
        /// <param name="configSource">The data source to load from.</param>
        /// <param name="disableFile">If true, do not load from file (only from environment).</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>Loaded configuration data.</returns>
        public static ClientConfig Load(
            DataSource? configSource = null,
            bool disableFile = false,
            bool configFileStrict = false,
            Dictionary<string, string>? overrideEnvVars = null)
        {
            var runtime = Runtime.TemporalRuntime.Default.Runtime;
            var profiles = Bridge.EnvConfig.LoadClientConfig(
                runtime,
                configSource ?? DataSource.FromDefault(),
                disableFile,
                configFileStrict,
                overrideEnvVars);
            return new ClientConfig(profiles);
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
            Dictionary<string, string>? overrideEnvVars = null)
        {
            var profileName = profile ?? "default";
            var clientProfile = ClientConfigProfile.Load(
                profileName,
                configSource,
                disableFile,
                disableEnv,
                configFileStrict,
                overrideEnvVars);
            return clientProfile.ToConnectionOptions();
        }

        /// <summary>
        /// Deserialize client configuration from JSON.
        /// </summary>
        /// <param name="json">JSON string to deserialize.</param>
        /// <param name="options">Optional JSON serializer options.</param>
        /// <returns>Client configuration instance.</returns>
        public static ClientConfig FromJson(string json, JsonSerializerOptions? options = null)
        {
            return JsonSerializer.Deserialize<ClientConfig>(json, options ?? GetDefaultJsonOptions())
                ?? throw new JsonException("Failed to deserialize ClientConfig from JSON.");
        }

        /// <summary>
        /// Serialize this client configuration to JSON.
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
            var newConfig = (ClientConfig)MemberwiseClone();
            var clonedProfiles = new Dictionary<string, ClientConfigProfile>();
            foreach (var kvp in Profiles)
            {
                clonedProfiles[kvp.Key] = (ClientConfigProfile)kvp.Value.Clone();
            }
            newConfig.Profiles = clonedProfiles;
            return newConfig;
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