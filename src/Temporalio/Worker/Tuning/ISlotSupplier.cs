#pragma warning disable CA1040 // We are ok with an empty interface here

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Slot suppliers control how slots are handed out for workflow and activity tasks as well as
    /// local activities when used in conjunction with a <see cref="WorkerTuner"/>.
    ///
    /// Currently you cannot implement your own slot supplier, but you can use the provided <see
    /// cref="FixedSizeSlotSupplier"/> and <see cref="ResourceBasedSlotSupplier"/> slot suppliers.
    /// </summary>
    public interface ISlotSupplier
    {
    }
}