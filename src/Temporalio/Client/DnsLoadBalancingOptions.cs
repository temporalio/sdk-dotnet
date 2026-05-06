using System;

namespace Temporalio.Client
{
    /// <summary>
    /// DNS-based load balancing options for Temporal connections.
    /// </summary>
    public class DnsLoadBalancingOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets a value indicating whether DNS-based load balancing is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets how often to re-resolve DNS.
        /// </summary>
        public TimeSpan ResolutionInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
