#pragma warning disable CA1040 // We are ok with an empty interface here

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Slot suppliers control how slots are handed out for workflow and activity tasks as well as
    /// local activities when used in conjunction with a <see cref="WorkerTuner"/>.
    ///
    /// Pre-built slot suppliers are available as
    /// <see cref="FixedSizeSlotSupplier"/> and <see cref="ResourceBasedSlotSupplier"/>.
    ///
    /// In order to implement your own slot supplier, you can extend the
    /// <see cref="CustomSlotSupplier"/> class.
    /// </summary>
    public abstract class SlotSupplier
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SlotSupplier"/> class.
        /// </summary>
        internal SlotSupplier()
        {
        }
    }
}
