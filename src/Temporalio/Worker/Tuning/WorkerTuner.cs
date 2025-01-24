namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Implements <see cref="IWorkerTuner"/> by holding the different <see cref="SlotSupplier"/>s.
    /// </summary>
    public class WorkerTuner : IWorkerTuner
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerTuner"/> class composed of the
        /// provided slot suppliers.
        /// </summary>
        /// <param name="workflowTaskSlotSupplier">The supplier of workflow task slots.</param>
        /// <param name="activityTaskSlotSupplier">The supplier of activity task slots.</param>
        /// <param name="localActivitySlotSupplier">The supplier of local activity slots.</param>
        /// <returns>The tuner.</returns>
        public WorkerTuner(
            SlotSupplier workflowTaskSlotSupplier,
            SlotSupplier activityTaskSlotSupplier,
            SlotSupplier localActivitySlotSupplier)
        {
            WorkflowTaskSlotSupplier = workflowTaskSlotSupplier;
            ActivityTaskSlotSupplier = activityTaskSlotSupplier;
            LocalActivitySlotSupplier = localActivitySlotSupplier;
        }

        /// <summary>
        /// Gets a slot supplier for workflow tasks.
        /// </summary>
        /// <returns>A slot supplier for workflow tasks.</returns>
        public SlotSupplier WorkflowTaskSlotSupplier { get; init; }

        /// <summary>
        /// Gets a slot supplier for activity tasks.
        /// </summary>
        /// <returns>A slot supplier for activity tasks.</returns>
        public SlotSupplier ActivityTaskSlotSupplier { get; init; }

        /// <summary>
        /// Gets a slot supplier for local activities.
        /// </summary>
        /// <returns>A slot supplier for local activities.</returns>
        public SlotSupplier LocalActivitySlotSupplier { get; init; }

        /// <summary>
        /// Create a resource based tuner with the provided options.
        /// </summary>
        /// <param name="targetMemoryUsage">A value between 0 and 1 that represents the target
        /// (system) memory usage. It's not recommended to set this higher than 0.8, since how much
        /// memory a workflow may use is not predictable, and you don't want to encounter OOM
        /// errors.</param>
        /// <param name="targetCpuUsage">A value between 0 and 1 that represents the target (system)
        /// CPU usage. This can be set to 1.0 if desired, but it's recommended to leave some
        /// headroom for other processes.</param>
        /// <param name="workflowOptions">Options for the workflow task slot supplier.</param>
        /// <param name="activityOptions">Options for the activity task slot supplier.</param>
        /// <param name="localActivityOptions">Options for the local activity slot supplier.</param>
        /// <returns>The tuner.</returns>
        /// <remarks>
        /// WARNING: Resource based tuning is currently experimental.
        /// </remarks>
        public static WorkerTuner CreateResourceBased(
            double targetMemoryUsage,
            double targetCpuUsage,
            ResourceBasedSlotSupplierOptions? workflowOptions = null,
            ResourceBasedSlotSupplierOptions? activityOptions = null,
            ResourceBasedSlotSupplierOptions? localActivityOptions = null)
        {
            ResourceBasedTunerOptions tunerOpts =
                new ResourceBasedTunerOptions(targetMemoryUsage, targetCpuUsage);
            return new(
                new ResourceBasedSlotSupplier(
                    workflowOptions == null
                        ? new ResourceBasedSlotSupplierOptions()
                        : workflowOptions,
                    tunerOpts),
                new ResourceBasedSlotSupplier(
                    activityOptions == null
                        ? new ResourceBasedSlotSupplierOptions()
                        : activityOptions,
                    tunerOpts),
                new ResourceBasedSlotSupplier(
                    localActivityOptions == null
                        ? new ResourceBasedSlotSupplierOptions()
                        : localActivityOptions,
                    tunerOpts));
        }

        /// <summary>
        /// Create a fixed-size tuner with the given slot capacities.
        /// </summary>
        /// <param name="workflowTaskSlots">The number of available workflow task slots.</param>
        /// <param name="activityTaskSlots">The number of available activity task slots.</param>
        /// <param name="localActivitySlots">The number of available local activity slots.</param>
        /// <returns>The tuner.</returns>
        public static WorkerTuner CreateFixedSize(
            int workflowTaskSlots,
            int activityTaskSlots,
            int localActivitySlots)
        {
            return new(
                new FixedSizeSlotSupplier(workflowTaskSlots),
                new FixedSizeSlotSupplier(activityTaskSlots),
                new FixedSizeSlotSupplier(localActivitySlots));
        }
    }
}
