namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for releasing a slot used from a <see cref="ICustomSlotSupplier"/>.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public class SlotReleaseContext
    {
        /// <summary>
        /// Info about the task that will be using the slot. May be null if the slot was never used.
        /// </summary>
        public SlotInfo? SlotInfo { get; }

        /// <summary>
        /// The permit that was issued when the slot was reserved.
        /// </summary>
        public SlotPermit Permit { get; }
    }
}
