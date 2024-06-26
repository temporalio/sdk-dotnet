namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// A slot supplier that will only ever issue at most a fixed number of slots.
    /// </summary>
    /// <param name="NumSlots">The maximum number of slots that will ever be issued.</param>
    public sealed record FixedSizeSlotSupplier(uint NumSlots) : ISlotSupplier;
}