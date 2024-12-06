namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Defines types of Slots that workers use.
    /// </summary>
    public enum SlotType
    {
        /// <summary>
        /// Workflow slot type.
        /// </summary>
        Workflow,

        /// <summary>
        /// Activity slot type.
        /// </summary>
        Activity,

        /// <summary>
        /// Local activity slot type.
        /// </summary>
        LocalActivity,
    }
}
