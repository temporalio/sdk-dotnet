using System;
using System.Threading.Tasks;

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// This class can be implemented to provide custom slot supplier behavior.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public interface ICustomSlotSupplier : ISlotSupplier
    {
        /// <summary>
        /// This function is called before polling for new tasks. Your implementation must block
        /// until a slot is available then return a permit to use that slot.
        /// The only acceptable exception to throw is <see cref="OperationCanceledException"/>, as
        /// invocations of this method may be cancelled. Any other exceptions thrown will be logged
        /// and ignored.
        /// </summary>
        /// <param name="ctx">The context for slot reservation.</param>
        /// <returns>A permit to use the slot which may be populated with your own data.</returns>
        /// <exception cref="OperationCanceledException">Cancellation requested.</exception>
        public Task<SlotPermit> ReserveSlotAsync(SlotReserveContext ctx);

        /// <summary>
        /// This function is called when trying to reserve slots for "eager" workflow and activity tasks.
        /// Eager tasks are those which are returned as a result of completing a workflow task, rather than
        /// from polling. Your implementation must not block, and if a slot is available, return a permit
        /// to use that slot.
        /// </summary>
        /// <param name="ctx">The context for slot reservation.</param>
        /// <returns>Maybe a permit to use the slot which may be populated with your own data.</returns>
        public SlotPermit? TryReserveSlot(SlotReserveContext ctx);

        /// <summary>
        /// This function is called once a slot is actually being used to process some task, which may be
        /// some time after the slot was reserved originally. For example, if there is no work for a
        /// worker, a number of slots equal to the number of active pollers may already be reserved, but
        /// none of them are being used yet. This call should be non-blocking.
        /// </summary>
        /// <param name="ctx">The context for marking a slot as used.</param>
        public void MarkSlotUsed(SlotMarkUsedContext ctx);

        /// <summary>
        /// This function is called once a permit is no longer needed. This could be because the task has
        /// finished, whether successfully or not, or because the slot was no longer needed (ex: the number
        /// of active pollers decreased). This call should be non-blocking.
        /// </summary>
        /// <param name="ctx">The context for releasing a slot.</param>
        public void ReleaseSlot(SlotReleaseContext ctx);
    }
}
