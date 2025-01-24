namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// A slot supplier that will only ever issue at most a fixed number of slots.
    /// </summary>
    public sealed class FixedSizeSlotSupplier : SlotSupplier
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FixedSizeSlotSupplier"/> class.
        /// </summary>
        /// <param name="slotCount">The maximum number of slots that will ever be issued.</param>
        public FixedSizeSlotSupplier(int slotCount)
        {
            SlotCount = slotCount;
        }

        /// <summary>
        /// Gets the maximum number of slots that will ever be issued.
        /// </summary>
        public int SlotCount { get; }
    }
}
