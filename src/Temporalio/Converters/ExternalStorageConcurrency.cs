using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// Limits on how many external storage requests may be in flight at once. Set on
    /// <see cref="ExternalStorage.Concurrency" />.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed record ExternalStorageConcurrency
    {
        private const int DefaultMaxDriverOperations = 64;
        private const int DefaultMaxOperationsPerMessage = 8;

        /// <summary>
        /// The default limits.
        /// </summary>
        public static readonly ExternalStorageConcurrency Default = new();

        private readonly int maxDriverOperations = DefaultMaxDriverOperations;
        private readonly int maxOperationsPerMessage = DefaultMaxOperationsPerMessage;

        /// <summary>
        /// Gets the maximum number of requests in flight at once across every driver registered on
        /// the owning <see cref="ExternalStorage" />. Defaults to 64.
        /// </summary>
        /// <remarks>
        /// Each worker and each client gets its own concurrency limit of this size.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">If set to less than 1.</exception>
        public int MaxDriverOperations
        {
            get => maxDriverOperations;
            init
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value, "MaxDriverOperations must be at least 1.");
                }
                maxDriverOperations = value;
            }
        }

        /// <summary>
        /// Gets the maximum number of requests in flight at once on behalf of a single message, such
        /// as one workflow task activation, activity task, Nexus operation, or client request.
        /// Defaults to 8.
        /// </summary>
        /// <remarks>
        /// Every message gets its own budget of this size, which caps how much of
        /// <see cref="MaxDriverOperations" /> any one message can take and so stops a message
        /// carrying many large payloads from starving the rest.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">If set to less than 1.</exception>
        public int MaxOperationsPerMessage
        {
            get => maxOperationsPerMessage;
            init
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value, "MaxOperationsPerMessage must be at least 1.");
                }
                maxOperationsPerMessage = value;
            }
        }
    }
}
