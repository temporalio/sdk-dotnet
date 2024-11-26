using Temporalio.Bridge;

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
        /// Initializes a new instance of the <see cref="SlotReserveContext"/> class.
        /// </summary>
        /// <param name="ctx">The bridge version of the slot reserve context.</param>
        internal SlotReserveContext(Temporalio.Bridge.Interop.SlotReserveCtx ctx)
        {
            this.SlotType = ctx.slot_type switch
            {
                Temporalio.Bridge.Interop.SlotKindType.WorkflowSlotKindType => SlotType.Workflow,
                Temporalio.Bridge.Interop.SlotKindType.ActivitySlotKindType => SlotType.Activity,
                Temporalio.Bridge.Interop.SlotKindType.LocalActivitySlotKindType => SlotType.LocalActivity,
                _ => throw new System.ArgumentOutOfRangeException(nameof(ctx)),
            };
            this.TaskQueue = ByteArrayRef.ToUtf8(ctx.task_queue);
            this.WorkerIdentity = ByteArrayRef.ToUtf8(ctx.worker_identity);
            this.WorkerBuildId = ByteArrayRef.ToUtf8(ctx.worker_build_id);
            this.IsSticky = ctx.is_sticky != 0;
        }

        /// <summary>
        /// Gets the type of slot trying to be reserved. Always one of "workflow", "activity", or "local-activity".
        /// </summary>
        public SlotType SlotType { get; }

        /// <summary>
        /// Gets the name of the task queue for which this reservation request is associated.
        /// </summary>
        public string TaskQueue { get; }

        /// <summary>
        /// Gets the identity of the worker that is requesting the reservation.
        /// </summary>
        public string WorkerIdentity { get; }

        /// <summary>
        /// Gets the build id of the worker that is requesting the reservation.
        /// </summary>
        public string WorkerBuildId { get; }

        /// <summary>
        /// Gets a value indicating whether true iff this is a reservation for a sticky poll for a workflow task.
        /// </summary>
        public bool IsSticky { get; }
    }
}
