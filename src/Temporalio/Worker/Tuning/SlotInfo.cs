using Temporalio.Bridge;

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Info about a task slot usage.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public record SlotInfo
    {
        private SlotInfo()
        {
        }

        /// <summary>
        /// Creates a <see cref="SlotInfo"/> from the bridge version.
        /// </summary>
        /// <param name="slot_info">The bridge version of the slot info.</param>
        /// <returns>The slot info.</returns>
        internal static SlotInfo FromBridge(Temporalio.Bridge.Interop.SlotInfo slot_info)
        {
            return slot_info.tag switch
            {
                Temporalio.Bridge.Interop.SlotInfo_Tag.WorkflowSlotInfo =>
                    new WorkflowSlotInfo(ByteArrayRef.ToUtf8(slot_info.workflow_slot_info.workflow_type), slot_info.workflow_slot_info.is_sticky != 0),
                Temporalio.Bridge.Interop.SlotInfo_Tag.ActivitySlotInfo =>
                    new ActivitySlotInfo(ByteArrayRef.ToUtf8(slot_info.activity_slot_info.activity_type)),
                Temporalio.Bridge.Interop.SlotInfo_Tag.LocalActivitySlotInfo =>
                    new LocalActivitySlotInfo(ByteArrayRef.ToUtf8(slot_info.local_activity_slot_info.activity_type)),
                _ => throw new System.ArgumentOutOfRangeException(nameof(slot_info)),
            };
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
