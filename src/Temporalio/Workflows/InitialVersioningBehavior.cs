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

        /// <summary>
        /// Use the Ramping Version of the workflow's task queue at start time, regardless of the
        /// workflow's Target Version. After the first workflow task completes, use whatever
        /// Versioning Behavior the workflow is annotated with in the workflow code. If there is no
        /// Ramping Version when the first workflow task is dispatched, the task goes to the Current
        /// Version.
        /// <para>
        /// This is discouraged for workflows annotated with AutoUpgrade behavior because this
        /// setting only applies to the first task of the new run.
        /// </para>
        /// <para>
        /// If the workflow being continued has a Pinned override, that override is inherited by the
        /// new run regardless of this setting.
        /// </para>
        /// </summary>
        UseRampingVersion = Temporalio.Api.Enums.V1.ContinueAsNewVersioningBehavior.UseRampingVersion,
    }
}
