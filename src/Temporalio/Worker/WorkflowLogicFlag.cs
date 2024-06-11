namespace Temporalio.Worker
{
    /// <summary>
    /// Flags that may be set on task/activation completion to differentiate new from old workflow
    /// behavior.
    /// </summary>
    internal enum WorkflowLogicFlag : uint
    {
        /// <summary>
        /// When set, this makes sure that workflow completion is moved to the end of the command
        /// set.
        /// </summary>
        ReorderWorkflowCompletion = 1,
    }
}