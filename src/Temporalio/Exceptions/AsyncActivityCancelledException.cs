namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown from <see cref="Client.AsyncActivityHandle.HeartbeatAsync" /> if workflow
    /// has requested that an activity be cancelled.
    /// </summary>
    public class AsyncActivityCancelledException : TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncActivityCancelledException"/> class.
        /// </summary>
        public AsyncActivityCancelledException()
            : base("Activity cancelled")
        {
        }
    }
}