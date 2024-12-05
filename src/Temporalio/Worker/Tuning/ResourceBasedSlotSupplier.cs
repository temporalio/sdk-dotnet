namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// A slot supplier that will dynamically adjust the number of slots based on resource usage.
    /// </summary>
    /// <remarks>
    /// WARNING: Resource based tuning is currently experimental.
    /// </remarks>
    public sealed class ResourceBasedSlotSupplier : SlotSupplier
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceBasedSlotSupplier"/> class.
        /// </summary>
        /// <param name="options">Options specific to the slot type this supplier is used for.</param>
        /// <param name="tunerOptions">Options for the tuner that will be used to adjust the number of slots.</param>
        public ResourceBasedSlotSupplier(ResourceBasedSlotSupplierOptions options, ResourceBasedTunerOptions tunerOptions)
        {
            Options = options;
            TunerOptions = tunerOptions;
        }

        /// <summary>
        /// Gets Options specific to the slot type this supplier is used for.
        /// </summary>
        public ResourceBasedSlotSupplierOptions Options { get; }

        /// <summary>
        /// Gets Options for the tuner that will be used to adjust the number of slots.
        /// All resource-based slot suppliers must use the same tuner options.
        /// </summary>
        public ResourceBasedTunerOptions TunerOptions { get; }
    }
}
