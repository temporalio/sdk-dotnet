using System;

namespace Temporalio.Client
{
    public class RpcRetryOptions
    {
        public TimeSpan InitialInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        public float RandomizationFactor { get; set; } = 0.2F;

        public float Multiplier { get; set; } = 1.5F;

        public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan? MaxElapsedTime { get; set; } = TimeSpan.FromSeconds(10);

        public int MaxRetries { get; set; } = 10;
    }
}
