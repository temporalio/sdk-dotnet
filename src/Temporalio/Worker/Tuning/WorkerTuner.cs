namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// TODO.
    /// </summary>
    public abstract class WorkerTuner
    {
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
        public static WorkerTuner CreateResourceBased(
            float targetMemoryUsage,
            float targetCpuUsage,
            ResourceBasedSlotSupplierOptions? workflowOptions = null,
            ResourceBasedSlotSupplierOptions? activityOptions = null,
            ResourceBasedSlotSupplierOptions? localActivityOptions = null)
        {
            ResourceBasedTunerOptions tunerOpts =
                new ResourceBasedTunerOptions(targetMemoryUsage, targetCpuUsage);
            return new CompositeTuner(
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
        /// <param name="numWorkflowTaskSlots">The number of available workflow task slots.</param>
        /// <param name="numActivityTaskSlots">The number of available activity task slots.</param>
        /// <param name="numLocalActivitySlots">The number of available local activity slots.</param>
        /// <returns>The tuner.</returns>
        public static WorkerTuner CreateFixed(
            uint numWorkflowTaskSlots,
            uint numActivityTaskSlots,
            uint numLocalActivitySlots)
        {
            return new CompositeTuner(
                new FixedSizeSlotSupplier(numWorkflowTaskSlots),
                new FixedSizeSlotSupplier(numActivityTaskSlots),
                new FixedSizeSlotSupplier(numLocalActivitySlots));
        }

        /// <summary>
        /// Create a tuner composed of the provided slot suppliers.
        /// </summary>
        /// <param name="workflowTaskSlotSupplier">The supplier of workflow task slots.</param>
        /// <param name="activityTaskSlotSupplier">The supplier of activity task slots.</param>
        /// <param name="localActivitySlotSupplier">The supplier of local activity slots.</param>
        /// <returns>The tuner.</returns>
        public static WorkerTuner CreateComposite(
            ISlotSupplier workflowTaskSlotSupplier,
            ISlotSupplier activityTaskSlotSupplier,
            ISlotSupplier localActivitySlotSupplier)
        {
            return new CompositeTuner(
                workflowTaskSlotSupplier,
                activityTaskSlotSupplier,
                localActivitySlotSupplier);
        }

        /// <summary>
        /// Gets a slot supplier for workflow tasks.
        /// </summary>
        /// <returns>A slot supplier for workflow tasks.</returns>
        public abstract ISlotSupplier GetWorkflowTaskSlotSupplier();

        /// <summary>
        /// Gets a slot supplier for activity tasks.
        /// </summary>
        /// <returns>A slot supplier for activity tasks.</returns>
        public abstract ISlotSupplier GetActivityTaskSlotSupplier();

        /// <summary>
        /// Gets a slot supplier for local activities.
        /// </summary>
        /// <returns>A slot supplier for local activities.</returns>
        public abstract ISlotSupplier GetLocalActivitySlotSupplier();
    }
}