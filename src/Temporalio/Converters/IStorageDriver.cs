using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Driver that stores and retrieves payloads in an external storage system.
    /// </summary>
    /// <remarks>
    /// Implementations are called concurrently, so they must be thread-safe.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal interface IStorageDriver
    {
        /// <summary>
        /// Gets the name of this driver instance. This is written into history alongside every
        /// payload the driver stores and is used to route retrieval back to the same driver.
        /// </summary>
        /// <remarks>
        /// Renaming a deployed driver makes every payload stored under the old name unretrievable,
        /// so this must be treated as permanent once in use.
        /// </remarks>
        string Name { get; }

        /// <summary>
        /// Gets the identifier for this driver implementation, e.g. <c>aws.s3driver</c>. Unlike
        /// <see cref="Name" />, this is identical across every instance of the same implementation
        /// and across SDK languages. It is only reported in worker heartbeats.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Store the given payloads, returning one claim per payload in the same order.
        /// </summary>
        /// <param name="context">Context for this store operation, carrying the limiter each
        /// request must be run through.</param>
        /// <param name="payloads">Payloads to store. Do not mutate these.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// Claims identifying the stored payloads. This must return exactly one claim per given
        /// payload, in the same order.
        /// </returns>
        /// <exception cref="System.OperationCanceledException">Cancellation requested.</exception>
        Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve the payloads for the given claims, in the same order.
        /// </summary>
        /// <param name="context">Context for this retrieve operation, carrying the limiter each
        /// request must be run through.</param>
        /// <param name="claims">Claims to retrieve. Do not mutate these.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// The stored payloads. This must return exactly one payload per given claim, in the same
        /// order.
        /// </returns>
        /// <exception cref="System.OperationCanceledException">Cancellation requested.</exception>
        Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken);
    }
}
