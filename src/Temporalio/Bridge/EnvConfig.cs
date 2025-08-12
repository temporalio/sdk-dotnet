using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Temporalio.Client.Configuration;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Bridge for environment configuration loading.
    /// </summary>
    internal static class EnvConfig
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Load client configuration from environment variables and configuration files.
        /// </summary>
        /// <param name="runtime">Runtime to use for the operation.</param>
        /// <param name="source">Data source to load configuration from.</param>
        /// <param name="disableFile">If true, do not load from file (only from environment).</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>Dictionary of profile name to client configuration profile.</returns>
        public static Dictionary<string, ClientConfigProfile> LoadClientConfig(
            Runtime runtime,
            DataSource source,
            bool disableFile = false,
            bool configFileStrict = false,
            Dictionary<string, string>? overrideEnvVars = null)
        {
            var task = LoadClientConfigAsync(
                runtime,
                source.Path,
                source.Data != null ? System.Text.Encoding.UTF8.GetString(source.Data) : null,
                disableFile,
                configFileStrict,
                overrideEnvVars);

            var result = RunTaskSafely(task);

            // Extract profiles from the config structure
            if (result.TryGetValue("profiles", out var profilesObj))
            {
                var typedProfiles = new Dictionary<string, ClientConfigProfile>();

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
                                profileElement.GetRawText(), JsonOptions);
                            if (profileDict != null)
                            {
                                typedProfiles[kvp.Key] = CreateClientConfigProfile(profileDict);
                            }
                        }
                    }
                }

                return typedProfiles;
            }

            // If no profiles found, return empty dictionary
            return new Dictionary<string, ClientConfigProfile>();
        }

        /// <summary>
        /// Loads a specific client configuration profile.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="profile">Profile name to load.</param>
        /// <param name="source">Data source to load configuration from.</param>
        /// <param name="disableFile">If true, do not load from file (only from environment).</param>
        /// <param name="disableEnv">If true, disable environment variable overrides.</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>Client configuration for the specified profile.</returns>
        public static ClientConfigProfile LoadClientConfigProfile(
            Runtime runtime,
            string profile,
            DataSource source,
            bool disableFile = false,
            bool disableEnv = false,
            bool configFileStrict = false,
            Dictionary<string, string>? overrideEnvVars = null)
        {
            var task = LoadClientConfigProfileAsync(
                runtime,
                profile,
                source.Path,
                source.Data != null ? System.Text.Encoding.UTF8.GetString(source.Data) : null,
                disableFile,
                disableEnv,
                configFileStrict,
                overrideEnvVars);

            var result = RunTaskSafely(task);

            return CreateClientConfigProfile(result);
        }

        /// <summary>
        /// Load client configuration from environment variables and configuration files.
        /// </summary>
        /// <param name="runtime">Runtime to use for the operation.</param>
        /// <param name="path">Path to the configuration file, or null to use default search paths.</param>
        /// <param name="data">Configuration data as a string, or null to load from file.</param>
        /// <param name="disableFile">If true, do not load from file (only from environment).</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>Dictionary of profile name to client configuration.</returns>
        public static async Task<Dictionary<string, Dictionary<string, object?>>> LoadClientConfigAsync(
            Runtime runtime,
            string? path = null,
            string? data = null,
            bool disableFile = false,
            bool configFileStrict = false,
            Dictionary<string, string>? overrideEnvVars = null)
        {
            using var scope = new Scope();
            var completion = new TaskCompletionSource<Dictionary<string, Dictionary<string, object?>>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // Create C strings that will be kept alive for the duration of the call
            var pathPtr = IntPtr.Zero;
            var strings = new List<IntPtr>();

            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    pathPtr = Marshal.StringToHGlobalAnsi(path);
                    strings.Add(pathPtr);
                }

                // Keep the callback delegate alive
                unsafe
                {
                    var callback = new Interop.TemporalCoreClientConfigCallback((userData, success, fail) =>
                    {
                        if (fail != null)
                        {
                            var errorMessage = new ByteArray(runtime, fail).ToUTF8();
                            completion.TrySetException(new InvalidOperationException($"Configuration loading error: {errorMessage}"));
                        }
                        else if (success != null)
                        {
                            try
                            {
                                var json = new ByteArray(runtime, success).ToUTF8();
                                var result = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object?>>>(
                                    json,
                                    JsonOptions);
                                completion.TrySetResult(result ?? new Dictionary<string, Dictionary<string, object?>>());
                            }
                            catch (JsonException ex)
                            {
                                completion.TrySetException(
                                    new InvalidOperationException($"Failed to deserialize client config: {ex.Message}"));
                            }
                        }
                        else
                        {
                            completion.TrySetResult(new Dictionary<string, Dictionary<string, object?>>());
                        }
                    });

                    // Convert envVars to JSON format if provided
                    var envVarsRef = overrideEnvVars?.Count > 0
                        ? scope.ByteArray(JsonSerializer.Serialize(overrideEnvVars, JsonOptions))
                        : ByteArrayRef.Empty.Ref;

                    Interop.Methods.temporal_core_client_config_load(
                        (sbyte*)pathPtr,
                        scope.ByteArray(data),
                        Convert.ToByte(disableFile),
                        Convert.ToByte(configFileStrict),
                        envVarsRef,
                        (void*)null,
                        Marshal.GetFunctionPointerForDelegate(callback));
                }

                return await completion.Task.ConfigureAwait(false);
            }
            finally
            {
                foreach (var ptr in strings)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        /// <summary>
        /// Loads a specific client configuration profile.
        /// </summary>
        /// <param name="runtime">Runtime to use.</param>
        /// <param name="profile">Profile name to load.</param>
        /// <param name="path">Optional path to configuration file.</param>
        /// <param name="data">Optional configuration data string.</param>
        /// <param name="disableFile">If true, do not load from file (only from environment).</param>
        /// <param name="disableEnv">If true, disable environment variable overrides.</param>
        /// <param name="configFileStrict">If true, fail if configuration file is invalid.</param>
        /// <param name="overrideEnvVars">Environment variables to use, or null to use system environment.</param>
        /// <returns>Client configuration for the specified profile.</returns>
        public static async Task<Dictionary<string, object?>> LoadClientConfigProfileAsync(
            Runtime runtime,
            string profile,
            string? path = null,
            string? data = null,
            bool disableFile = false,
            bool disableEnv = false,
            bool configFileStrict = false,
            Dictionary<string, string>? overrideEnvVars = null)
        {
            using var scope = new Scope();
            var completion = new TaskCompletionSource<Dictionary<string, object?>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // Create C strings that will be kept alive for the duration of the call
            var strings = new List<IntPtr>();
            var profilePtr = IntPtr.Zero;
            var pathPtr = IntPtr.Zero;

            try
            {
                profilePtr = Marshal.StringToHGlobalAnsi(profile);
                strings.Add(profilePtr);

                if (!string.IsNullOrEmpty(path))
                {
                    pathPtr = Marshal.StringToHGlobalAnsi(path);
                    strings.Add(pathPtr);
                }

                // Keep the callback delegate alive
                unsafe
                {
                    var callback = new Interop.TemporalCoreClientConfigCallback((userData, success, fail) =>
                    {
                        if (fail != null)
                        {
                            var errorMessage = new ByteArray(runtime, fail).ToUTF8();
                            completion.TrySetException(new InvalidOperationException($"Configuration loading error: {errorMessage}"));
                        }
                        else if (success != null)
                        {
                            try
                            {
                                var json = new ByteArray(runtime, success).ToUTF8();
                                var result = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                    json,
                                    JsonOptions);
                                completion.TrySetResult(result ?? new Dictionary<string, object?>());
                            }
                            catch (JsonException ex)
                            {
                                completion.TrySetException(
                                    new InvalidOperationException($"Failed to deserialize client config profile: {ex.Message}"));
                            }
                        }
                        else
                        {
                            completion.TrySetResult(new Dictionary<string, object?>());
                        }
                    });

                    // Convert envVars to JSON format if provided
                    var envVarsRef = overrideEnvVars?.Count > 0
                        ? scope.ByteArray(JsonSerializer.Serialize(overrideEnvVars, JsonOptions))
                        : ByteArrayRef.Empty.Ref;

                    Interop.Methods.temporal_core_client_config_profile_load(
                        (sbyte*)profilePtr,
                        (sbyte*)pathPtr,
                        scope.ByteArray(data),
                        Convert.ToByte(disableFile),
                        Convert.ToByte(disableEnv),
                        Convert.ToByte(configFileStrict),
                        envVarsRef,
                        (void*)null,
                        Marshal.GetFunctionPointerForDelegate(callback));
                }

                return await completion.Task.ConfigureAwait(false);
            }
            finally
            {
                foreach (var ptr in strings)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        private static T RunTaskSafely<T>(Task<T> task)
        {
#pragma warning disable VSTHRD002, VSTHRD003 // We know we aren't deadlocking here and Task.Run is safe
            return Task.Run(() => task).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002, VSTHRD003
        }

        /// <summary>
        /// Creates a ClientConfigProfile from a dictionary of configuration values.
        /// </summary>
        /// <param name="profileData">The profile data dictionary.</param>
        /// <returns>A new ClientConfigProfile instance.</returns>
        private static ClientConfigProfile CreateClientConfigProfile(Dictionary<string, object?> profileData)
        {
            return new ClientConfigProfile(
                GetString(profileData, "address"),
                GetString(profileData, "namespace"),
                GetString(profileData, "api_key"),
                CreateTlsConfig(profileData),
                CreateGrpcMeta(profileData));
        }

        private static string? GetString(Dictionary<string, object?> data, string key) =>
            data.TryGetValue(key, out var value) ? value?.ToString() : null;

        private static ClientConfigTls? CreateTlsConfig(Dictionary<string, object?> profileData)
        {
            if (!profileData.TryGetValue("tls", out var tlsObj))
            {
                return null;
            }

            var tlsData = tlsObj switch
            {
                Dictionary<string, object?> dict => dict,
                JsonElement el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText(), JsonOptions),
                _ => null,
            };

            if (tlsData == null)
            {
                return null;
            }

            var disabled = tlsData.TryGetValue("disabled", out var disabledObj) && disabledObj switch
            {
                JsonElement el => el.GetBoolean(),
                bool b => b,
                _ => Convert.ToBoolean(disabledObj),
            };

            return new ClientConfigTls(
                GetString(tlsData, "server_name"),
                GetDataSource(tlsData, "server_ca_cert"),
                GetDataSource(tlsData, "client_cert"),
                GetDataSource(tlsData, "client_private_key") ?? GetDataSource(tlsData, "client_key"))
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
                JsonElement el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText(), JsonOptions),
                _ => null,
            };

            return metaData?.Where(kv => kv.Value != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.ToString() ?? string.Empty);
        }

        private static DataSource? GetDataSource(Dictionary<string, object?> data, string key) =>
            data.TryGetValue(key, out var value) && value != null ? ParseDataSource(value) : null;

        private static DataSource? ParseDataSource(object dataSourceObj)
        {
            var dataSourceDict = dataSourceObj switch
            {
                Dictionary<string, object?> dict => dict,
                JsonElement el => JsonSerializer.Deserialize<Dictionary<string, object?>>(el.GetRawText(), JsonOptions),
                _ => null,
            };

            if (dataSourceDict != null)
            {
                if (dataSourceDict.TryGetValue("path", out var pathObj) && pathObj?.ToString() is string path)
                {
                    return DataSource.FromPath(path);
                }
                if (dataSourceDict.TryGetValue("data", out var dataObj) && dataObj?.ToString() is string data)
                {
                    return DataSource.FromString(data);
                }
            }

            var stringValue = dataSourceObj.ToString();
            return !string.IsNullOrEmpty(stringValue) ? DataSource.FromString(stringValue) : null;
        }
    }
}
