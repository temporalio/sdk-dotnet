namespace Temporalio.Worker.Tuning
{
    internal sealed record ResourceBasedSlotSupplier(
        ResourceBasedSlotSupplierOptions Options,
        ResourceBasedTunerOptions TunerOptions) : ISlotSupplier;
}