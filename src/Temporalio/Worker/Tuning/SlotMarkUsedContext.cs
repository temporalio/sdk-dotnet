namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for marking a slot used from a <see cref="ICustomSlotSupplier"/>.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public class SlotMarkUsedContext
    {
        /// <summary>
        /// Info about the task that will be using the slot.
        /// </summary>
        public SlotInfo SlotInfo { get; }

        /// <summary>
        /// The permit that was issued when the slot was reserved.
        /// </summary>
        public SlotPermit Permit { get; }
    }
}
