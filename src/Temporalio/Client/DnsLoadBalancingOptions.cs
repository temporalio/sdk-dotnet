using System;

namespace Temporalio.Client
{
    /// <summary>
    /// DNS load balancing options for Temporal connections.
    /// </summary>
    public class DnsLoadBalancingOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the interval at which DNS resolution is performed to refresh the set of
        /// addresses used for load balancing.
        /// </summary>
        public TimeSpan ResolutionInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
