using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Context given to <see cref="IStorageDriver.StoreAsync" />.
    /// </summary>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal sealed class StorageDriverStoreContext
    {
        /// <summary>
        /// Gets the execution the payloads are being stored on behalf of, or null if there is no
        /// associated execution.
        /// </summary>
        public IStorageDriverTargetInfo? Target { get; init; }

        /// <summary>
        /// Gets the limiter each store request is run through. Internal until concurrency limiting
        /// is part of the public API.
        /// </summary>
        internal IStorageDriverLimiter<Payload> Limiter { get; init; } =
            UnboundedStorageDriverLimiter<Payload>.Instance;
    }
}
