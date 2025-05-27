namespace Temporalio.Activities
{
    /// <summary>
    /// Boxed reference wrapper for <see cref="ActivityCancelReason" /> and
    /// <see cref="ActivityCancellationDetails"/>.
    /// </summary>
    internal class ActivityCancelRef
    {
        /// <summary>
        /// Gets or sets the cancel reason. Default is <see cref="ActivityCancelReason.None" />.
        /// </summary>
        public ActivityCancelReason CancelReason { get; set; } = ActivityCancelReason.None;

        /// <summary>
        /// Gets or sets the cancel details.
        /// </summary>
        public ActivityCancellationDetails? CancellationDetails { get; set; }
    }
}