namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Allows for the combination of different slot suppliers into one tuner.
    /// </summary>
    internal class CompositeTuner : WorkerTuner
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeTuner"/> class.
        /// </summary>
        /// <param name="workflowTaskSlotSupplier">The workflow task slot supplier.</param>
        /// <param name="activityTaskSlotSupplier">The activity task slots supplier.</param>
        /// <param name="localActivitySlotSupplier">The local activity slot supplier.</param>
        public CompositeTuner(
            ISlotSupplier workflowTaskSlotSupplier,
            ISlotSupplier activityTaskSlotSupplier,
            ISlotSupplier localActivitySlotSupplier)
        {
            this.WorkflowTaskSlotSupplier = workflowTaskSlotSupplier;
            this.ActivityTaskSlotSupplier = activityTaskSlotSupplier;
            this.LocalActivitySlotSupplier = localActivitySlotSupplier;
        }

        private ISlotSupplier WorkflowTaskSlotSupplier { get; init; }

        private ISlotSupplier ActivityTaskSlotSupplier { get; init; }

        private ISlotSupplier LocalActivitySlotSupplier { get; init; }

        /// <inheritdoc/>
        public override ISlotSupplier GetWorkflowTaskSlotSupplier() => WorkflowTaskSlotSupplier;

        /// <inheritdoc/>
        public override ISlotSupplier GetActivityTaskSlotSupplier() => ActivityTaskSlotSupplier;

        /// <inheritdoc/>
        public override ISlotSupplier GetLocalActivitySlotSupplier() => LocalActivitySlotSupplier;
    }
}