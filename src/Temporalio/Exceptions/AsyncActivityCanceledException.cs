namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown from <see cref="Client.AsyncActivityHandle.HeartbeatAsync" /> if workflow
    /// has requested that an activity be cancelled.
    /// </summary>
    public class AsyncActivityCanceledException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncActivityCanceledException"/> class.
        /// </summary>
        public AsyncActivityCanceledException()
            : base("Activity cancelled")
        {
        }
    }
}