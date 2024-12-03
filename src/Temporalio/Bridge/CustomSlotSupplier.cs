using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Bridge.Interop;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core wrapper for a user-defined custom slot supplier.
    /// </summary>
    internal class CustomSlotSupplier : NativeInvokeableClass<Interop.CustomSlotSupplierCallbacks>
    {
        private readonly ILogger logger;
        private readonly Temporalio.Worker.Tuning.ICustomSlotSupplier userSupplier;
        private readonly Dictionary<uint, GCHandle> permits = new();
        private uint permitId = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSlotSupplier" /> class.
        /// </summary>
        /// <param name="userSupplier">User's slot supplier implementation'.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        internal unsafe CustomSlotSupplier(
            Temporalio.Worker.Tuning.ICustomSlotSupplier userSupplier,
            ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<CustomSlotSupplier>();
            this.userSupplier = userSupplier;

            var interopCallbacks = new Interop.CustomSlotSupplierCallbacks
            {
                reserve = FunctionPointer<Interop.CustomReserveSlotCallback>(Reserve),
                cancel_reserve = FunctionPointer<Interop.CustomCancelReserveCallback>(CancelReserve),
                try_reserve = FunctionPointer<Interop.CustomTryReserveSlotCallback>(TryReserve),
                mark_used = FunctionPointer<Interop.CustomMarkSlotUsedCallback>(MarkUsed),
                release = FunctionPointer<Interop.CustomReleaseSlotCallback>(Release),
            };

            PinCallbackHolder(interopCallbacks);
        }

        private static void SetCancelTokenOnCtx(ref SlotReserveCtx ctx, CancellationTokenSource cancelTokenSrc)
        {
            unsafe
            {
                try
                {
                    var handle = GCHandle.Alloc(cancelTokenSrc);
                    fixed (Interop.SlotReserveCtx* p = &ctx)
                    {
                        Interop.Methods.set_reserve_cancel_target(p, GCHandle.ToIntPtr(handle).ToPointer());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error setting cancel token on ctx: {e}");
                    throw;
                }
            }
        }

        private unsafe void Reserve(Interop.SlotReserveCtx ctx, void* sender)
        {
            SafeReserve(ctx, new IntPtr(sender));
        }

        private unsafe void CancelReserve(void* tokenSrc)
        {
            var handle = GCHandle.FromIntPtr(new IntPtr(tokenSrc));
            var cancelTokenSrc = (CancellationTokenSource)handle.Target!;
            cancelTokenSrc.Cancel();
            handle.Free();
        }

        private void SafeReserve(Interop.SlotReserveCtx ctx, IntPtr sender)
        {
            var reserveTask = Task.Run(async () =>
            {
                var cancelTokenSrc = new System.Threading.CancellationTokenSource();
                SetCancelTokenOnCtx(ref ctx, cancelTokenSrc);
                while (true)
                {
                    try
                    {
                        var permit = await userSupplier.ReserveSlotAsync(
                            new(ctx), cancelTokenSrc.Token).ConfigureAwait(false);
                        var usedPermitId = AddPermitToMap(permit);
                        unsafe
                        {
                            Interop.Methods.complete_async_reserve(sender.ToPointer(), new(usedPermitId));
                        }
                        cancelTokenSrc.Dispose();
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        cancelTokenSrc.Dispose();
                        return;
                    }
#pragma warning disable CA1031 // We are ok catching all exceptions here
                    catch (Exception e)
                    {
#pragma warning restore CA1031
                        logger.LogError(e, "Error reserving slot");
                    }
                    // Wait for a bit to avoid spamming errors
                    await Task.Delay(1000, cancelTokenSrc.Token).ConfigureAwait(false);
                }
            });
        }

        private unsafe UIntPtr TryReserve(Interop.SlotReserveCtx ctx)
        {
            Temporalio.Worker.Tuning.ISlotPermit? maybePermit;
            try
            {
                maybePermit = userSupplier.TryReserveSlot(new(ctx));
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error trying to reserve slot");
                return UIntPtr.Zero;
            }

            if (maybePermit == null)
            {
                return UIntPtr.Zero;
            }
            var usedPermitId = AddPermitToMap(maybePermit);
            return new(usedPermitId);
        }

        private void MarkUsed(Interop.SlotMarkUsedCtx ctx)
        {
            try
            {
                userSupplier.MarkSlotUsed(new(ctx, permits[ctx.slot_permit.ToUInt32()]));
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error marking slot used");
            }
        }

        private void Release(Interop.SlotReleaseCtx ctx)
        {
            var permitId = ctx.slot_permit.ToUInt32();
            try
            {
                userSupplier.ReleaseSlot(new(ctx, permits[permitId]));
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error releasing slot");
            }
            permits.Remove(permitId);
        }

        private uint AddPermitToMap(Temporalio.Worker.Tuning.ISlotPermit permit)
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
