namespace Temporalio.Activities
{
    /// <summary>
    /// Boxed reference wrapper for <see cref="ActivityCancelReason" />.
    /// </summary>
    internal class ActivityCancelReasonRef
    {
        /// <summary>
        /// Gets or sets the cancel reason.
        /// </summary>
        public ActivityCancelReason CancelReason { get; set; } = ActivityCancelReason.None;
    }
}