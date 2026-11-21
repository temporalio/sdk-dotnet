using System;
using Amazon.SecretsManager;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// Options for <see cref="SecretsManagerExtension" />.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    public class SecretsManagerExtensionOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the Secrets Manager client used to fetch secrets. If unset, secrets are
        /// fetched through the AWS Parameters and Secrets Lambda Extension if it is detected, and
        /// otherwise with a client created from the default AWS config.
        /// </summary>
        public IAmazonSecretsManager? SecretsClient { get; set; }

        /// <summary>
        /// Gets or sets how long a fetched secret is used before it is refreshed. If unset,
        /// defaults to the <c>SECRETS_MANAGER_TTL</c> environment variable (non-negative whole
        /// seconds, where 0 refreshes on every use) as used by the AWS Parameters and Secrets
        /// Lambda Extension, or 5 minutes if that is unset.
        /// </summary>
        public TimeSpan? RefreshInterval { get; set; }

        /// <summary>
        /// Gets or sets how long after its last successful fetch a secret may still be used if
        /// refreshing it fails. Defaults to 24 hours. Values less than the refresh interval are
        /// treated as the refresh interval.
        /// </summary>
        public TimeSpan? MaxStaleness { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
