using System;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Converters
{
    /// <summary>
    /// Bounds how many external storage requests are in flight at once. An instance is handed to a
    /// driver on <see cref="StorageDriverStoreContext" /> and
    /// <see cref="StorageDriverRetrieveContext" />.
    /// </summary>
    /// <typeparam name="TItem">
    /// What a single request covers: a <see cref="Api.Common.V1.Payload" /> when storing, a
    /// <see cref="StorageDriverClaim" /> when retrieving.
    /// </typeparam>
    /// <remarks>
    /// The SDK cannot see the requests a driver makes, so the limit works only if the driver calls
    /// <see cref="RunAsync{TResult}" /> for each request.
    /// </remarks>
    /// <remarks>
    /// Wrapping an entire fan-out in a single permit satisfies the signature while quietly
    /// defeating the limit.
    /// </remarks>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal interface IStorageDriverLimiter<in TItem>
    {
        /// <summary>
        /// Run the given operation once a permit for the given item is available, releasing the
        /// permit when it completes.
        /// </summary>
        /// <typeparam name="TResult">Result of the operation.</typeparam>
        /// <param name="item">The payload or claim this request covers.</param>
        /// <param name="operation">The request to run while holding the permit.</param>
        /// <param name="cancellationToken">
        /// Cancellation token for waiting on the permit as well as for the operation itself.
        /// </param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="OperationCanceledException">Cancellation requested.</exception>
        /// <remarks>
        /// Do not take more than one permit for a given request: at the limit, every holder would
        /// be waiting for a permit only another holder can release.
        /// </remarks>
        Task<TResult> RunAsync<TResult>(
            TItem item,
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken);
    }
}
