namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown by client when attempting to start a standalone activity that was already
    /// started.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityAlreadyStartedException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityAlreadyStartedException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="activityId">See <see cref="ActivityId"/>.</param>
        /// <param name="activityType">See <see cref="ActivityType"/>.</param>
        /// <param name="runId">See <see cref="RunId"/>.</param>
        public ActivityAlreadyStartedException(
            string message, string activityId, string activityType, string? runId)
            : base(message)
        {
            ActivityId = activityId;
            ActivityType = activityType;
            RunId = runId;
        }

        /// <summary>
        /// Gets the activity ID that was already started.
        /// </summary>
        public string ActivityId { get; private init; }

        /// <summary>
        /// Gets the activity type that was attempted to start.
        /// </summary>
        public string ActivityType { get; private init; }

        /// <summary>
        /// Gets the run ID of the already-started activity, if available.
        /// </summary>
        public string? RunId { get; private init; }
    }
}
