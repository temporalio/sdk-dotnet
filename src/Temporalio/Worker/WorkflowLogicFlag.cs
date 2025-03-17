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

        /// <summary>
        /// Before this flag, workflow async logic did the following (in no particular order):
        /// * Reorder jobs itself.
        /// * Schedule primary workflow method when the initialize job was seen.
        /// * Tick the event loop after each job processed.
        /// * Check all conditions at once and resolve all that are satisfied.
        /// We have since learned that all of these are wrong. So if this flag is set on the first
        /// activation/task of a workflow, the following logic now applies respectively:
        /// * Leave Core's job ordering as is.
        /// * Do not schedule the primary workflow method until after all jobs have been applied and
        ///   it's not already present.
        /// * Tick the event loop only once after everything applied.
        /// * Only resolve the first condition that is satisfied and re-run the entire event loop.
        /// </summary>
        ApplyModernEventLoopLogic = 2,
    }
}