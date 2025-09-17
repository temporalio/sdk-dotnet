using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
        /// Creates a ClientConfigProfile from a dictionary of configuration values.
        /// </summary>
        /// <param name="profileData">The profile data dictionary.</param>
        /// <returns>A new ClientConfigProfile instance.</returns>
        private static ClientEnvConfig.ConfigProfile CreateClientConfigProfile(Dictionary<string, object?> profileData)
        {
            return new ClientEnvConfig.ConfigProfile(
                GetString(profileData, "address"),
                GetString(profileData, "namespace"),
                GetString(profileData, "api_key"),
                CreateTlsConfig(profileData),
                CreateGrpcMeta(profileData));
        }

        private static string? GetString(Dictionary<string, object?> data, string key) =>
            data.TryGetValue(key, out var value) ? value?.ToString() : null;

        private static ClientEnvConfig.Tls? CreateTlsConfig(Dictionary<string, object?> profileData)
        {
            if (!profileData.TryGetValue("tls", out var tlsObj))
            {
                return null;
            }

            var tlsData = tlsObj switch
            {
                Dictionary<string, object?> dict => dict,
                JsonElement el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText()),
                _ => null,
            };

            if (tlsData == null)
            {
                return null;
            }

            bool? disabled = null;
            if (tlsData.TryGetValue("disabled", out var disabledObj))
            {
                disabled = disabledObj switch
                {
                    JsonElement el => el.GetBoolean(),
                    bool b => b,
                    _ => Convert.ToBoolean(disabledObj),
                };
            }

            return new ClientEnvConfig.Tls(
                ServerName: GetString(tlsData, "server_name"),
                ServerRootCACert: GetDataSource(tlsData, "server_ca_cert"),
                ClientCert: GetDataSource(tlsData, "client_cert"),
                ClientPrivateKey: GetDataSource(tlsData, "client_private_key") ?? GetDataSource(tlsData, "client_key"))
            {
                Disabled = disabled,
            };
        }

        private static Dictionary<string, string>? CreateGrpcMeta(Dictionary<string, object?> profileData)
        {
            if (!profileData.TryGetValue("grpc_meta", out var metaObj) || metaObj == null)
            {
                return null;
            }

            var metaData = metaObj switch
            {
                Dictionary<string, object?> dict => dict,
                JsonElement el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText()),
                _ => null,
            };

            return metaData?.Where(kv => kv.Value != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.ToString() ?? string.Empty);
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

            var configDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object?>>>(
                json) ?? new Dictionary<string, Dictionary<string, object?>>();

            if (configDict.TryGetValue("profiles", out var profilesObj))
            {
                var typedProfiles = new Dictionary<string, ClientEnvConfig.ConfigProfile>();

                if (profilesObj is Dictionary<string, object?> profiles)
                {
                    foreach (var kvp in profiles)
                    {
                        if (kvp.Value is Dictionary<string, object?> profile)
                        {
                            typedProfiles[kvp.Key] = CreateClientConfigProfile(profile);
                        }
                        else if (kvp.Value is JsonElement profileElement)
                        {
                            var profileDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                profileElement.GetRawText());
                            if (profileDict != null)
                            {
                                typedProfiles[kvp.Key] = CreateClientConfigProfile(profileDict);
                            }
                        }
                    }
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

            var profileDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                json) ?? new Dictionary<string, object?>();

            return CreateClientConfigProfile(profileDict);
        }

        private static DataSource? GetDataSource(Dictionary<string, object?> data, string key) =>
            data.TryGetValue(key, out var value) && value != null ? ParseDataSource(value) : null;

        private static DataSource? ParseDataSource(object dataSourceObj)
        {
            var dataSourceDict = dataSourceObj switch
            {
                Dictionary<string, object?> dict => dict,
                JsonElement el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText()),
                _ => null,
            };

            if (dataSourceDict != null)
            {
                if (dataSourceDict.TryGetValue("path", out var pathObj) && pathObj?.ToString() is string path)
                {
                    return DataSource.FromPath(path);
                }
                if (dataSourceDict.TryGetValue("data", out var dataObj) && dataObj != null)
                {
                    // Handle both string data and byte array data from Rust Vec<u8>
                    return dataObj switch
                    {
                        JsonElement el when el.ValueKind == JsonValueKind.Array =>
                            DataSource.FromBytes(el.EnumerateArray().Select(x => (byte)x.GetInt32()).ToArray()),
                        JsonElement el when el.ValueKind == JsonValueKind.String =>
                            DataSource.FromUTF8String(el.GetString() ?? string.Empty),
                        string str => DataSource.FromUTF8String(str),
                        byte[] bytes => DataSource.FromBytes(bytes),
                        _ => dataObj.ToString() is string dataStr ? DataSource.FromUTF8String(dataStr) : null,
                    };
                }
            }

            var stringValue = dataSourceObj.ToString();
            return !string.IsNullOrEmpty(stringValue) ? DataSource.FromUTF8String(stringValue) : null;
        }
    }
}
