namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for reserving a slot from a <see cref="ICustomSlotSupplier"/>.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public class SlotReserveContext
    {
        /// <summary>
        /// Gets or sets the type of slot trying to be reserved. Always one of "workflow", "activity", or "local-activity".
        /// </summary>
        public SlotType SlotType { get; set; }

        /// <summary>
        /// Gets or sets the name of the task queue for which this reservation request is associated.
        /// </summary>
        public string TaskQueue { get; set; }

        /// <summary>
        /// Gets or sets the identity of the worker that is requesting the reservation.
        /// </summary>
        public string WorkerIdentity { get; set; }

        /// <summary>
        /// Gets or sets the build id of the worker that is requesting the reservation.
        /// </summary>
        public string WorkerBuildId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether true iff this is a reservation for a sticky poll for a workflow task.
        /// </summary>
        public bool IsSticky { get; set; }
    }
}
