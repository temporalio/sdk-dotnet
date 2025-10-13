namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Info about a task slot usage.
    /// </summary>
    public record SlotInfo
    {
        private SlotInfo()
        {
        }

        /// <summary>
        /// Info about a workflow task slot usage.
        /// </summary>
        public record WorkflowSlotInfo(string WorkflowType, bool IsSticky) : SlotInfo();
        /// <summary>
        /// Info about an activity task slot usage.
        /// </summary>
        public record ActivitySlotInfo(string ActivityType) : SlotInfo();
        /// <summary>
        /// Info about a local activity task slot usage.
        /// </summary>
        public record LocalActivitySlotInfo(string ActivityType) : SlotInfo();
    }
}
