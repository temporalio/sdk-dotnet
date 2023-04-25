namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown by client when attempting to create a schedule that was already created.
    /// </summary>
    public class ScheduleAlreadyRunningException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduleAlreadyRunningException"/> class.
        /// </summary>
        public ScheduleAlreadyRunningException()
            : base("Schedule already running")
        {
        }
    }
}