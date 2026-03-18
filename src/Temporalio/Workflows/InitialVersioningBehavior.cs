namespace Temporalio.Workflows
{
    /// <summary>
    /// Specifies the versioning behavior for the first task of a new workflow run in a
    /// continue-as-new chain.
    /// </summary>
    /// <remarks>WARNING: Worker deployment based versioning is currently experimental.</remarks>
    public enum InitialVersioningBehavior
    {
        /// <summary>
        /// Unspecified versioning behavior; inherits from the previous run.
        /// </summary>
        Unspecified = Temporalio.Api.Enums.V1.ContinueAsNewVersioningBehavior.Unspecified,

        /// <summary>
        /// Start the new run with AutoUpgrade behavior. Use the Target Version of the workflow's
        /// task queue at start-time, as AutoUpgrade workflows do. After the first workflow task
        /// completes, use whatever Versioning Behavior the workflow is annotated with in the
        /// workflow code.
        /// </summary>
        AutoUpgrade = Temporalio.Api.Enums.V1.ContinueAsNewVersioningBehavior.AutoUpgrade,
    }
}
