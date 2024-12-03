using System.Runtime.InteropServices;

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Context for releasing a slot used from a <see cref="ICustomSlotSupplier"/>.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public class SlotReleaseContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SlotReleaseContext"/> class.
        /// </summary>
        /// <param name="ctx">The bridge version of the slot release context.</param>
        /// <param name="userData">The user data associated with the slot.</param>
        internal SlotReleaseContext(Temporalio.Bridge.Interop.SlotReleaseCtx ctx, GCHandle userData)
        {
            unsafe
            {
                this.SlotInfo = ctx.slot_info is null ? null : SlotInfo.FromBridge(*ctx.slot_info);
                this.Permit = (ISlotPermit)userData.Target!;
            }
        }

        /// <summary>
        /// Gets info about the task that will be using the slot. May be null if the slot was never used.
        /// </summary>
        public SlotInfo? SlotInfo { get; }

        /// <summary>
        /// Gets the permit that was issued when the slot was reserved.
        /// </summary>
        public ISlotPermit Permit { get; }
    }
}
