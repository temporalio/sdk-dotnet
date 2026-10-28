namespace Temporalio.Converters
{
    /// <summary>
    /// Context given to <see cref="IStorageDriver.RetrieveAsync" />.
    /// </summary>
    /// <remarks>
    /// This deliberately carries no target. A payload must be retrievable from its
    /// <see cref="StorageDriverClaim" /> alone, since the same payload can be read from a different
    /// execution than the one that stored it.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class StorageDriverRetrieveContext
    {
        /// <summary>
        /// Gets the limiter each retrieve request is run through. Internal until concurrency
        /// limiting is part of the public API.
        /// </summary>
        internal IStorageDriverLimiter<StorageDriverClaim> Limiter { get; init; } =
            UnboundedStorageDriverLimiter<StorageDriverClaim>.Instance;
    }
}
