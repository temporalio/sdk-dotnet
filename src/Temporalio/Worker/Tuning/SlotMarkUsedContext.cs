namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for marking a slot used from a <see cref="CustomSlotSupplier"/>.
    /// </summary>
    /// <param name="SlotInfo">Info about the task that will be using the slot.</param>
    /// <param name="Permit">The permit that was issued when the slot was reserved.</param>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record SlotMarkUsedContext(SlotInfo SlotInfo, SlotPermit Permit);
}
