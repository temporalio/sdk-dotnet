using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Client config extension that loads client credentials (API key and/or mTLS material) from
    /// AWS Secrets Manager.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pass to <see cref="ClientEnvConfig.ProfileLoadOptions.Extensions" /> and connect with
    /// <see cref="TemporalClient.ConnectAsync" />. Secret IDs are configured in the
    /// <c>profile.{profile}.ext.aws_secrets_manager</c> TOML table or with
    /// <c>TEMPORAL_EXT_AWS_SECRETS_MANAGER_*</c> environment variables. See the package README for
    /// details.
    /// </para>
    /// <para>WARNING: This API is experimental and may change in the future.</para>
    /// </remarks>
    public sealed class SecretsManagerExtension : IClientConfigExtension
    {
        /// <summary>
        /// The name of the extension, which is also its table key under
        /// <c>profile.{profile}.ext</c>.
        /// </summary>
        public const string ExtensionName = "aws_secrets_manager";

        private const string ApiKeyIdEnvVar = "TEMPORAL_EXT_AWS_SECRETS_MANAGER_API_KEY_ID";
        private const string ClientCertIdEnvVar = "TEMPORAL_EXT_AWS_SECRETS_MANAGER_TLS_CLIENT_CERT_ID";
        private const string ClientKeyIdEnvVar = "TEMPORAL_EXT_AWS_SECRETS_MANAGER_TLS_CLIENT_KEY_ID";
        private const string ServerCACertIdEnvVar = "TEMPORAL_EXT_AWS_SECRETS_MANAGER_TLS_SERVER_CA_CERT_ID";

        // Same env var and default as the AWS Parameters and Secrets Lambda Extension's cache TTL
        private const string SecretsManagerTtlEnvVar = "SECRETS_MANAGER_TTL";
        private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultMaxStaleness = TimeSpan.FromHours(24);
        private static readonly Regex WholeNumber = new("^[0-9]+$");

        private readonly SecretsManagerExtensionOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecretsManagerExtension"/> class.
        /// </summary>
        /// <param name="options">Options, or null for the defaults.</param>
        public SecretsManagerExtension(SecretsManagerExtensionOptions? options = null) =>
            this.options = (SecretsManagerExtensionOptions?)options?.Clone() ?? new();

        /// <summary>
        /// Gets the extension name, <c>aws_secrets_manager</c>.
        /// </summary>
        public string Name => ExtensionName;

        /// <summary>
        /// Gets or sets how process environment variables are read. Settable so that tests can
        /// replace them.
        /// </summary>
        internal Func<string, string?> GetEnvironmentVariable { get; set; } =
            Environment.GetEnvironmentVariable;

        /// <summary>
        /// Read secret IDs from the profile and env, and create a plugin that fetches the
        /// configured secrets.
        /// </summary>
        /// <param name="context">The loaded profile and env.</param>
        /// <returns>The plugin, or null if no secret IDs are configured.</returns>
        public ITemporalClientPlugin? CreateClientPlugin(ClientConfigExtensionContext context)
        {
            var ids = LoadSecretIds(context);
            if (ids.IsEmpty)
            {
                return null;
            }

            var refreshInterval = GetRefreshInterval();
            var maxStaleness = options.MaxStaleness is { } staleness && staleness > TimeSpan.Zero ?
                staleness : DefaultMaxStaleness;
            if (maxStaleness < refreshInterval)
            {
                maxStaleness = refreshInterval;
            }

            IFetcher fetcher = options.SecretsClient != null ?
                new SdkFetcher(options.SecretsClient) :
                Fetchers.NewDefaultFetcher(GetEnvironmentVariable);
            return new SecretsManagerPlugin(
                ids, new SecretCache(fetcher, ids, refreshInterval, maxStaleness));
        }

        private static SecretIds LoadSecretIds(ClientConfigExtensionContext context)
        {
            var fields = new Dictionary<string, string>();
            if (context.Profile.Ext is { } ext && ext.TryGetValue(ExtensionName, out var table))
            {
                ApplyToml(fields, table);
            }
            if (context.Env is { } env)
            {
                // Like envconfig, set-but-empty env vars override (and so can unset) file values
                foreach (var (name, field) in new[]
                {
                    (ApiKeyIdEnvVar, "api_key"),
                    (ClientCertIdEnvVar, "client_cert"),
                    (ClientKeyIdEnvVar, "client_key"),
                    (ServerCACertIdEnvVar, "server_ca_cert"),
                })
                {
                    if (env.TryGetValue(name, out var value))
                    {
                        fields[field] = value;
                    }
                }
            }
            string Field(string name) => fields.TryGetValue(name, out var value) ? value : string.Empty;
            var ids = new SecretIds(
                Field("api_key"), Field("client_cert"), Field("client_key"), Field("server_ca_cert"));
            Validate(ids, context.Profile);
            return ids;
        }

        private static void ApplyToml(Dictionary<string, string> fields, JsonElement table)
        {
            if (table.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException(
                    $"ext.{ExtensionName} must be a table, got {TypeName(table)}");
            }
            foreach (var prop in table.EnumerateObject())
            {
                if (prop.Name == "api_key_id")
                {
                    fields["api_key"] = TomlString(prop.Name, prop.Value);
                }
                else if (prop.Name == "tls")
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object)
                    {
                        throw new ArgumentException(
                            $"ext.{ExtensionName}.tls must be a table, got {TypeName(prop.Value)}");
                    }
                    foreach (var tlsProp in prop.Value.EnumerateObject())
                    {
                        var field = tlsProp.Name switch
                        {
                            "client_cert_id" => "client_cert",
                            "client_key_id" => "client_key",
                            "server_ca_cert_id" => "server_ca_cert",
                            _ => throw new ArgumentException(
                                $"unknown key \"{tlsProp.Name}\" in ext.{ExtensionName}.tls"),
                        };
                        fields[field] = TomlString($"tls.{tlsProp.Name}", tlsProp.Value);
                    }
                }
                else
                {
                    throw new ArgumentException($"unknown key \"{prop.Name}\" in ext.{ExtensionName}");
                }
            }
        }

        private static string TomlString(string key, JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException(
                    $"ext.{ExtensionName}.{key} must be a string, got {TypeName(value)}");
            }
            return value.GetString()!;
        }

        private static string TypeName(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Object => "table",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            _ => "null",
        };

        private static void Validate(SecretIds ids, ClientEnvConfig.Profile profile)
        {
            var conflicts = new List<string>();
            if (ids.ApiKey.Length > 0 && !string.IsNullOrEmpty(profile.ApiKey))
            {
                conflicts.Add("API key secret ID cannot be set with an API key");
            }
            if (profile.Tls is { } tls)
            {
                if (ids.ClientCert.Length > 0 && tls.ClientCert != null)
                {
                    conflicts.Add("TLS client cert secret ID cannot be set with client cert path or data");
                }
                if (ids.ClientKey.Length > 0 && tls.ClientPrivateKey != null)
                {
                    conflicts.Add("TLS client key secret ID cannot be set with client key path or data");
                }
                if (ids.ServerCACert.Length > 0 && tls.ServerRootCACert != null)
                {
                    conflicts.Add(
                        "TLS server CA cert secret ID cannot be set with server CA cert path or data");
                }
                if (ids.HasTls && tls.Disabled == true)
                {
                    conflicts.Add("TLS secret IDs cannot be set when TLS is disabled");
                }
            }
            if ((ids.ClientCert.Length > 0) != (ids.ClientKey.Length > 0))
            {
                conflicts.Add("TLS client cert and key secret IDs must be set together");
            }
            if (conflicts.Count > 0)
            {
                throw new ArgumentException(
                    $"invalid config: {string.Join("; ", conflicts.OrderBy(c => c, StringComparer.Ordinal))}");
            }
        }

        private TimeSpan GetRefreshInterval()
        {
            if (options.RefreshInterval is { } interval)
            {
                if (interval < TimeSpan.Zero)
                {
                    throw new ArgumentException("RefreshInterval must not be negative");
                }
                return interval;
            }
            // Read from the process env rather than the config env, like
            // PARAMETERS_SECRETS_EXTENSION_HTTP_PORT, since this is Lambda runtime config shared
            // with the Lambda extension rather than Temporal client config
            var ttl = GetEnvironmentVariable(SecretsManagerTtlEnvVar);
            if (string.IsNullOrEmpty(ttl))
            {
                return DefaultRefreshInterval;
            }
            if (WholeNumber.IsMatch(ttl) && long.TryParse(ttl, out var seconds) &&
                seconds <= TimeSpan.MaxValue.TotalSeconds)
            {
                return TimeSpan.FromSeconds(seconds);
            }
            throw new ArgumentException(
                $"invalid {SecretsManagerTtlEnvVar} \"{ttl}\", must be a non-negative whole number of seconds");
        }
    }
}
