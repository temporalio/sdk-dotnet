using System;
using System.Threading;
using System.Threading.Tasks;

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// This class can be implemented to provide custom slot supplier behavior.
    /// </summary>
    public abstract class CustomSlotSupplier : SlotSupplier
    {
        /// <summary>
        /// This function is called before polling for new tasks. Your implementation must block
        /// until a slot is available then return a permit to use that slot.
        /// The only acceptable exception to throw is <see cref="OperationCanceledException"/>, as
        /// invocations of this method may be cancelled. Any other exceptions thrown will be logged
        /// and ignored. If an exception is thrown, this method will be called again after a one
        /// second wait, since it must block until a permit is returned.
        /// </summary>
        /// <remarks>
        /// This method will be called concurrently from multiple threads, so it must be thread-safe.
        /// </remarks>
        /// <param name="ctx">The context for slot reservation.</param>
        /// <param name="cancellationToken">A cancellation token that the SDK may use
        ///  to cancel the operation.</param>
        /// <returns>A permit to use the slot which may be populated with your own data.</returns>
        /// <exception cref="OperationCanceledException">Cancellation requested.</exception>
        public abstract Task<SlotPermit> ReserveSlotAsync(SlotReserveContext ctx, CancellationToken cancellationToken);

        /// <summary>
        /// This function is called when trying to reserve slots for "eager" workflow and activity tasks.
        /// Eager tasks are those which are returned as a result of completing a workflow task, rather than
        /// from polling. Your implementation must not block, and if a slot is available, return a permit
        /// to use that slot.
        /// </summary>
        /// <remarks>
        /// This method will be called concurrently from multiple threads, so it must be thread-safe.
        /// </remarks>
        /// <remarks>
        /// Any exceptions thrown will be logged and ignored.
        /// </remarks>
        /// <param name="ctx">The context for slot reservation.</param>
        /// <returns>Maybe a permit to use the slot which may be populated with your own data.</returns>
        public abstract SlotPermit? TryReserveSlot(SlotReserveContext ctx);

        /// <summary>
        /// This function is called once a slot is actually being used to process some task, which may be
        /// some time after the slot was reserved originally. For example, if there is no work for a
        /// worker, a number of slots equal to the number of active pollers may already be reserved, but
        /// none of them are being used yet. This call should be non-blocking.
        /// </summary>
        /// <remarks>
        /// This method will be called concurrently from multiple threads, so it must be thread-safe.
        /// </remarks>
        /// <remarks>
        /// Any exceptions thrown will be logged and ignored.
        /// </remarks>
        /// <param name="ctx">The context for marking a slot as used.</param>
        public abstract void MarkSlotUsed(SlotMarkUsedContext ctx);

        /// <summary>
        /// This function is called once a permit is no longer needed. This could be because the task has
        /// finished, whether successfully or not, or because the slot was no longer needed (ex: the number
        /// of active pollers decreased). This call should be non-blocking.
        /// </summary>
        /// <remarks>
        /// This method will be called concurrently from multiple threads, so it must be thread-safe.
        /// </remarks>
        /// <remarks>
        /// Any exceptions thrown will be logged and ignored.
        /// </remarks>
        /// <param name="ctx">The context for releasing a slot.</param>
        public abstract void ReleaseSlot(SlotReleaseContext ctx);
    }
}
