using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Keep alive options for Temporal connections.
    /// </summary>
    public class KeepAliveOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the interval to send HTTP2 keep alive pings.
        /// </summary>
        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the timeout that the keep alive must be responded to within or the
        /// connection will be closed.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}