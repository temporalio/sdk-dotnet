using System.Threading.Tasks;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core wrapper for a user-defined custom slot supplier.
    /// </summary>
    internal class CustomSlotSupplier : NativeInvokeableClass<Interop.CustomSlotSupplierCallbacks>
    {
        private readonly Temporalio.Worker.Tuning.ICustomSlotSupplier userSupplier;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSlotSupplier" /> class.
        /// </summary>
        /// <param name="userSupplier">User's slot supplier implementation'.</param>
        internal unsafe CustomSlotSupplier(Temporalio.Worker.Tuning.ICustomSlotSupplier userSupplier)
        {
            this.userSupplier = userSupplier;

            var interopCallbacks = new Interop.CustomSlotSupplierCallbacks
            {
                reserve = FunctionPointer<Interop.CustomReserveSlotCallback>(Reserve),
                try_reserve = FunctionPointer<Interop.CustomTryReserveSlotCallback>(TryReserve),
                mark_used = FunctionPointer<Interop.CustomMarkSlotUsedCallback>(MarkUsed),
                release = FunctionPointer<Interop.CustomReleaseSlotCallback>(Release),
            };

            PinCallbackHolder(interopCallbacks);
        }

        private void Reserve(Interop.SlotReserveCtx ctx)
        {
            // TODO: Need to call callback with result that will put it in a channel to await in Rust
            var reserveTask = Task.Run(() => userSupplier.ReserveSlotAsync(new(ctx)));
        }

        private void TryReserve(Interop.SlotReserveCtx ctx)
        {
            userSupplier.TryReserveSlot(new(ctx));
        }

        private void MarkUsed(Interop.SlotMarkUsedCtx ctx)
        {
            userSupplier.MarkSlotUsed(new(ctx));
        }

        private void Release(Interop.SlotReleaseCtx ctx)
        {
            userSupplier.ReleaseSlot(new(ctx));
        }
    }
}
