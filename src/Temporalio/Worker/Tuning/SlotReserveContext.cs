namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for reserving a slot from a <see cref="CustomSlotSupplier"/>.
    /// </summary>
    /// <param name="SlotType">The type of slot trying to be reserved.</param>
    /// <param name="TaskQueue">The name of the task queue for which this reservation request is associated.</param>
    /// <param name="WorkerIdentity">The identity of the worker that is requesting the reservation.</param>
    /// <param name="WorkerBuildId">The build id of the worker that is requesting the reservation.</param>
    /// <param name="IsSticky">True iff this is a reservation for a sticky poll for a workflow task.</param>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record SlotReserveContext(
        SlotType SlotType,
        string TaskQueue,
        string WorkerIdentity,
        string WorkerBuildId,
        bool IsSticky);
}
