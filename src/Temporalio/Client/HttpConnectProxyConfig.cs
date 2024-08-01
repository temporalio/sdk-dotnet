using System;

namespace Temporalio.Client
{
    /// <summary>
    /// HTTP connect proxy options for Temporal connections.
    /// </summary>
    public class HttpConnectProxyConfig : ICloneable
    {
        /// <summary>
        /// Gets or sets the target host to proxy through as a host:port string.
        /// </summary>
        public string? TargetHost { get; set; }

        /// <summary>
        /// Gets or sets HTTP basic auth for the proxy.
        /// </summary>
        public (string Username, string Password)? BasicAuth { get; set; }

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}