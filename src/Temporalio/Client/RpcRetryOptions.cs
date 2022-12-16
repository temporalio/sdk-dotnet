using System;

namespace Temporalio.Client
{
    /// <summary>
    /// Retry options that can be set at the connection level to apply on calls that are retried.
    /// </summary>
    public class RpcRetryOptions : ICloneable
    {
        /// <summary>
        /// Gets or sets the initial retry interval.
        /// </summary>
        public TimeSpan InitialInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Gets or sets the randomization factor.
        /// </summary>
        public float RandomizationFactor { get; set; } = 0.2F;

        /// <summary>
        /// Gets or sets the multiplier.
        /// </summary>
        public float Multiplier { get; set; } = 1.5F;

        /// <summary>
        /// Gets or sets the max interval.
        /// </summary>
        public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the max elapsed time.
        /// </summary>
        public TimeSpan? MaxElapsedTime { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the max retries.
        /// </summary>
        public int MaxRetries { get; set; } = 10;

        /// <summary>
        /// Create a shallow copy of these options.
        /// </summary>
        /// <returns>A shallow copy of these options.</returns>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
