using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core wrapper for a user-defined custom slot supplier.
    /// </summary>
    internal class CustomSlotSupplier : NativeInvokeableClass<Interop.CustomSlotSupplierCallbacks>
    {
        private readonly Temporalio.Worker.Tuning.ICustomSlotSupplier userSupplier;
        private readonly Dictionary<uint, GCHandle> permits = new();
        private uint permitId = 1;

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

        private unsafe void Reserve(Interop.SlotReserveCtx ctx, void* sender)
        {
            SafeReserve(ctx, new IntPtr(sender));
        }

        private void SafeReserve(Interop.SlotReserveCtx ctx, IntPtr sender)
        {
            var reserveTask = Task.Run(async () =>
            {
                try
                {
                    var permit = await userSupplier.ReserveSlotAsync(new(ctx)).ConfigureAwait(false);
                    var usedPermitId = AddPermitToMap(permit);
                    unsafe
                    {
                        Interop.Methods.complete_async_reserve(sender.ToPointer(), new(usedPermitId));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception in reserve: " + e.Message);
                    throw;
                }
            });
        }

        private unsafe UIntPtr TryReserve(Interop.SlotReserveCtx ctx)
        {
            var maybePermit = userSupplier.TryReserveSlot(new(ctx));
            if (maybePermit == null)
            {
                return UIntPtr.Zero;
            }
            var usedPermitId = AddPermitToMap(maybePermit);
            return new(usedPermitId);
        }

        private void MarkUsed(Interop.SlotMarkUsedCtx ctx)
        {
            userSupplier.MarkSlotUsed(new(ctx, permits[ctx.slot_permit.ToUInt32()]));
        }

        private void Release(Interop.SlotReleaseCtx ctx)
        {
            var permitId = ctx.slot_permit.ToUInt32();
            userSupplier.ReleaseSlot(new(ctx, permits[permitId]));
            permits.Remove(permitId);
        }

        private uint AddPermitToMap(Temporalio.Worker.Tuning.SlotPermit permit)
        {
            var handle = GCHandle.Alloc(permit);
            lock (permits)
            {
                var usedPermitId = permitId;
                permits.Add(permitId, handle);
                permitId += 1;
                return usedPermitId;
            }
        }
    }
}
