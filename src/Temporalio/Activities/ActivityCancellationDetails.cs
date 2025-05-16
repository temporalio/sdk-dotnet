namespace Temporalio.Activities
{
    /// <summary>
    /// Details that are set when an activity is cancelled. This is only valid at the time the
    /// cancel was received, the state may change on the server after it is received.
    /// </summary>
    public class ActivityCancellationDetails
    {
        /// <summary>
        /// Gets a value indicating whether the activity no longer exists on the server (may already
        /// be completed or its workflow may be completed).
        /// </summary>
        public bool IsGoneFromServer { get; init; }

        /// <summary>
        /// Gets a value indicating whether the activity was explicitly cancelled.
        /// </summary>
        public bool IsCancelRequested { get; init; }

        /// <summary>
        /// Gets a value indicating whether the activity timeout caused activity to be marked
        /// cancelled.
        /// </summary>
        public bool IsTimedOut { get; init; }

        /// <summary>
        /// Gets a value indicating whether the worker the activity is running on is shutting down.
        /// </summary>
        public bool IsWorkerShutdown { get; init; }

        /// <summary>
        /// Gets a value indicating whether the activity was explicitly paused.
        /// </summary>
        public bool IsPaused { get; init; }

        /// <summary>
        /// Gets a value indicating whether the activity failed to record heartbeat. This usually
        /// only happens if the details cannot be converted to payloads.
        /// </summary>
        public bool IsHeartbeatRecordFailure { get; init; }
    }
}