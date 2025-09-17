using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Temporalio.Client.EnvConfig;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Bridge for environment configuration loading.
    /// </summary>
    internal static class EnvConfig
    {
        /// <summary>
        /// Load client configuration from environment variables and configuration files.
        /// </summary>
        /// <param name="runtime">Runtime to use for the operation.</param>
        /// <param name="options">Options for loading the configuration.</param>
        /// <returns>Dictionary of profile name to client configuration profile.</returns>
        public static Dictionary<string, ClientEnvConfig.ConfigProfile> LoadClientConfig(
            Runtime runtime,
            ClientEnvConfig.ConfigLoadOptions options)
        {
            using var scope = new Scope();

            try
            {
                var envVarsRef = options.OverrideEnvVars?.Count > 0
                    ? scope.ByteArray(JsonSerializer.Serialize(options.OverrideEnvVars))
                    : ByteArrayRef.Empty.Ref;

                unsafe
                {
                    var coreOptions = new Interop.TemporalCoreClientConfigLoadOptions
                    {
                        path = scope.ByteArray(options.ConfigSource?.Path),
                        data = scope.ByteArray(options.ConfigSource?.Data),
                        config_file_strict = Convert.ToByte(options.ConfigFileStrict),
                        env_vars = envVarsRef,
                    };

                    var result = Interop.Methods.temporal_core_client_config_load(&coreOptions);
                    return ProcessAllProfilesResult(runtime, result);
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to deserialize client config: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a specific client configuration profile.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="options">Options for loading the configuration profile.</param>
        /// <returns>Client configuration for the specified profile.</returns>
        public static ClientEnvConfig.ConfigProfile LoadClientConfigProfile(
            Runtime runtime,
            ClientEnvConfig.ProfileLoadOptions options)
        {
            using var scope = new Scope();

            try
            {
                var envVarsRef = options.OverrideEnvVars?.Count > 0
                    ? scope.ByteArray(JsonSerializer.Serialize(options.OverrideEnvVars))
                    : ByteArrayRef.Empty.Ref;

                unsafe
                {
                    var coreOptions = new Interop.TemporalCoreClientConfigProfileLoadOptions
                    {
                        profile = scope.ByteArray(options.Profile),
                        path = scope.ByteArray(options.ConfigSource?.Path),
                        data = scope.ByteArray(options.ConfigSource?.Data),
                        disable_file = Convert.ToByte(options.DisableFile),
                        disable_env = Convert.ToByte(options.DisableEnv),
                        config_file_strict = Convert.ToByte(options.ConfigFileStrict),
                        env_vars = envVarsRef,
                    };

                    var result = Interop.Methods.temporal_core_client_config_profile_load(&coreOptions);
                    return ProcessSingleProfileResult(runtime, result);
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to deserialize client config profile: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a ClientConfigProfile from typed JSON data.
        /// </summary>
        /// <param name="profileJson">The profile JSON data.</param>
        /// <returns>A new ClientConfigProfile instance.</returns>
        private static ClientEnvConfig.ConfigProfile CreateClientConfigProfile(ProfileJson profileJson)
        {
            return new ClientEnvConfig.ConfigProfile(
                profileJson.Address,
                profileJson.Namespace,
                profileJson.ApiKey,
                CreateTlsConfig(profileJson.Tls),
                profileJson.GrpcMeta);
        }

        private static ClientEnvConfig.Tls? CreateTlsConfig(TlsJson? tlsJson)
        {
            if (tlsJson == null)
            {
                return null;
            }

            return new ClientEnvConfig.Tls(
                ServerName: tlsJson.ServerName,
                ServerRootCACert: CreateDataSource(tlsJson.ServerCaCert),
                ClientCert: CreateDataSource(tlsJson.ClientCert),
                ClientPrivateKey: CreateDataSource(tlsJson.ClientKey))
            {
                Disabled = tlsJson.Disabled,
            };
        }

        /// <summary>
        /// Creates a DataSource from typed JSON data.
        /// </summary>
        /// <param name="dataSourceJson">The data source JSON data.</param>
        /// <returns>A new DataSource instance, or null if the input is null.</returns>
        private static DataSource? CreateDataSource(DataSourceJson? dataSourceJson)
        {
            if (dataSourceJson == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(dataSourceJson.Path))
            {
                return DataSource.FromPath(dataSourceJson.Path!);
            }

            if (dataSourceJson.Data != null && dataSourceJson.Data.Length > 0)
            {
                // Convert int[] from Rust Vec<u8> to byte[]
                var bytes = dataSourceJson.Data.Select(x => (byte)x).ToArray();
                return DataSource.FromBytes(bytes);
            }

            return null;
        }

        private static unsafe Dictionary<string, ClientEnvConfig.ConfigProfile> ProcessAllProfilesResult(
            Runtime runtime,
            Interop.TemporalCoreClientConfigOrFail result)
        {
            if (result.fail != null)
            {
                string errorMessage;
                using (var byteArray = new ByteArray(runtime, result.fail))
                {
                    errorMessage = byteArray.ToUTF8();
                }
                throw new InvalidOperationException($"Configuration loading error: {errorMessage}");
            }

            if (result.success == null)
            {
                throw new InvalidOperationException("Failed to load client config: no success or failure result");
            }

            string json;
            using (var byteArray = new ByteArray(runtime, result.success))
            {
                json = byteArray.ToUTF8();
            }

            var configDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ProfileJson>>>(
                json) ?? new Dictionary<string, Dictionary<string, ProfileJson>>();

            if (configDict.TryGetValue("profiles", out var profiles))
            {
                var typedProfiles = new Dictionary<string, ClientEnvConfig.ConfigProfile>();

                foreach (var kvp in profiles)
                {
                    typedProfiles[kvp.Key] = CreateClientConfigProfile(kvp.Value);
                }

                return typedProfiles;
            }

            return new Dictionary<string, ClientEnvConfig.ConfigProfile>();
        }

        private static unsafe ClientEnvConfig.ConfigProfile ProcessSingleProfileResult(
            Runtime runtime,
            Interop.TemporalCoreClientConfigProfileOrFail result)
        {
            if (result.fail != null)
            {
                string errorMessage;
                using (var byteArray = new ByteArray(runtime, result.fail))
                {
                    errorMessage = byteArray.ToUTF8();
                }
                throw new InvalidOperationException($"Configuration loading error: {errorMessage}");
            }

            if (result.success == null)
            {
                throw new InvalidOperationException("Failed to load client config profile: no success or failure result");
            }

            string json;
            using (var byteArray = new ByteArray(runtime, result.success))
            {
                json = byteArray.ToUTF8();
            }

            var profileJson = JsonSerializer.Deserialize<ProfileJson>(json) ?? new ProfileJson(null, null, null, null, null);

            return CreateClientConfigProfile(profileJson);
        }

        /// <summary>
        /// JSON DTO for client configuration profile from Rust core.
        /// </summary>
        internal record ProfileJson(
            [property: JsonPropertyName("address")] string? Address,
            [property: JsonPropertyName("namespace")] string? Namespace,
            [property: JsonPropertyName("api_key")] string? ApiKey,
            [property: JsonPropertyName("tls")] TlsJson? Tls,
            [property: JsonPropertyName("grpc_meta")] Dictionary<string, string>? GrpcMeta);

        /// <summary>
        /// JSON DTO for TLS configuration from Rust core.
        /// </summary>
        internal record TlsJson(
            [property: JsonPropertyName("disabled")] bool? Disabled,
            [property: JsonPropertyName("server_name")] string? ServerName,
            [property: JsonPropertyName("server_ca_cert")] DataSourceJson? ServerCaCert,
            [property: JsonPropertyName("client_cert")] DataSourceJson? ClientCert,
            [property: JsonPropertyName("client_key")] DataSourceJson? ClientKey);

        /// <summary>
        /// JSON DTO for data source from Rust core.
        /// </summary>
        internal record DataSourceJson(
            [property: JsonPropertyName("path")] string? Path,
            [property: JsonPropertyName("data")] int[]? Data);
    }
}
