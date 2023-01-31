namespace Temporalio.Activity
{
    /// <summary>
    /// Exception the user should throw to inform the worker that this activity will be completed
    /// asynchronously.
    /// </summary>
    public class CompleteAsyncException : Exceptions.TemporalException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompleteAsyncException"/> class.
        /// </summary>
        public CompleteAsyncException()
        {
        }
    }
}