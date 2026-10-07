namespace Temporalio.Exceptions
{
    /// <summary>
    /// Exception thrown when a child workflow cannot start because its versioning override is
    /// invalid.
    /// </summary>
    public class InvalidVersioningOverrideException : FailureException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidVersioningOverrideException"/> class.
        /// </summary>
        /// <param name="message">Error message.</param>
        public InvalidVersioningOverrideException(string message)
            : base(message)
        {
        }
    }
}
