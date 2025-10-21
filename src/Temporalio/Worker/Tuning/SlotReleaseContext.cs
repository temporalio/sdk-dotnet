namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for releasing a slot used from a <see cref="CustomSlotSupplier"/>.
    /// </summary>
    /// <param name="SlotInfo">Info about the task that will be using the slot. May be null if the slot was never used.</param>
    /// <param name="Permit">The permit that was issued when the slot was reserved.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record SlotReleaseContext(SlotInfo? SlotInfo, SlotPermit Permit);
}
