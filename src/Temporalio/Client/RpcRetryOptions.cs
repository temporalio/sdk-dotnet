using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Retry options that can be set at the connection level to apply on calls that are retried.
    /// </summary>
    public class RpcRetryOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the initial retry interval. Default is 100ms.
        /// </summary>
        public TimeSpan InitialInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Gets or sets the randomization factor. Default is 0.2.
        /// </summary>
        public float RandomizationFactor { get; set; } = 0.2F;

        /// <summary>
        /// Gets or sets the multiplier. Default is 1.5.
        /// </summary>
        public float Multiplier { get; set; } = 1.5F;

        /// <summary>
        /// Gets or sets the max interval. Default is 5s.
        /// </summary>
        public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the max elapsed time (or null for none). Default is 10s.
        /// </summary>
        public TimeSpan? MaxElapsedTime { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the max retries. Default is 10.
        /// </summary>
        public int MaxRetries { get; set; } = 10;

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone() => MemberwiseClone();
    }
}
