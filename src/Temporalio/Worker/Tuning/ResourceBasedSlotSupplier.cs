namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// A slot supplier that will dynamically adjust the number of slots based on resource usage.
    /// </summary>
    /// <param name="Options">Options specific to the slot type this supplier is used for.</param>
    /// <param name="TunerOptions">Options for the tuner that will be used to adjust the number of
    /// slots. All resource-based slot suppliers must use the same tuner options.</param>
    /// <remarks>
    /// WARNING: Resource based tuning is currently experimental.
    /// </remarks>
    public sealed record ResourceBasedSlotSupplier(
        ResourceBasedSlotSupplierOptions Options,
        ResourceBasedTunerOptions TunerOptions) : ISlotSupplier;
}