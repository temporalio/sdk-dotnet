namespace Temporalio.Activities
{
    /// <summary>
    /// Reason for why an activity may be cancelled.
    /// </summary>
    public enum ActivityCancelReason
    {
        /// <summary>
        /// The activity has not been cancelled or was cancelled for an unknown reason.
        /// </summary>
        None,

        /// <summary>
        /// The activity no longer exists on the server (may already be completed or its workflow
        /// may be completed).
        /// </summary>
        GoneFromServer,

        /// <summary>
        /// Activity was explicitly cancelled.
        /// </summary>
        CancelRequested,

        /// <summary>
        /// Activity timeout caused activity to be marked cancelled.
        /// </summary>
        Timeout,

        /// <summary>
        /// Worker the activity is running on is shutting down.
        /// </summary>
        WorkerShutdown,

        /// <summary>
        /// Failed to record heartbeat. This usually only happens if the details cannot be converted
        /// to payloads.
        /// </summary>
        HeartbeatRecordFailure,

        /// <summary>
        /// Activity was explicitly paused.
        /// </summary>
        Paused,
    }
}