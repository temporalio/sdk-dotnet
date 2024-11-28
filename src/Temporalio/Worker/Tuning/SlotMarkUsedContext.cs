using System.Runtime.InteropServices;

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for marking a slot used from a <see cref="ICustomSlotSupplier"/>.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public class SlotMarkUsedContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SlotMarkUsedContext"/> class.
        /// </summary>
        /// <param name="ctx">The bridge version of the slot mark used context.</param>
        /// <param name="userData">The user data associated with the slot.</param>
        internal SlotMarkUsedContext(Temporalio.Bridge.Interop.SlotMarkUsedCtx ctx, GCHandle userData)
        {
            this.SlotInfo = SlotInfo.FromBridge(ctx.slot_info);
            unsafe
            {
                this.Permit = (SlotPermit)userData.Target!;
            }
        }

        /// <summary>
        /// Gets info about the task that will be using the slot.
        /// </summary>
        public SlotInfo SlotInfo { get; }

        /// <summary>
        /// Gets the permit that was issued when the slot was reserved.
        /// </summary>
        public SlotPermit Permit { get; }
    }
}
