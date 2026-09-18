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
    /// A driver is handed a whole batch and fans out however it likes, so the SDK cannot bound that
    /// fan-out itself. The limit is cooperative: a driver that never calls
    /// <see cref="RunAsync{TResult}" /> is simply unlimited.
    /// </remarks>
    /// <remarks>
    /// Take one permit per concurrent request, not one around the whole batch. Wrapping an entire
    /// fan-out in a single permit satisfies the signature but defeats the limit.
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
        /// Taking a permit from inside another permit can deadlock once the nesting depth reaches
        /// the configured limit, so a driver must not nest these.
        /// </remarks>
        Task<TResult> RunAsync<TResult>(
            TItem item,
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken);
    }
}
