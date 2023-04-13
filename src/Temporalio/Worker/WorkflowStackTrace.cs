namespace Temporalio.Worker
{
    /// <summary>
    /// Represents the form of workflow stack traces supported.
    /// </summary>
    public enum WorkflowStackTrace
    {
        /// <summary>
        /// Workflow stack traces are unsupported and an error will occur when a request is made.
        /// </summary>
        None,

        /// <summary>
        /// Workflow stack traces are supported with file info.
        /// </summary>
        Normal,

        /// <summary>
        /// Workflow stack traces are supported, but without file info.
        /// </summary>
        WithoutFileInfo,
    }
}