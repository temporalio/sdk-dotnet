using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core wrapper for a user-defined custom slot supplier.
    /// </summary>
    internal class CustomSlotSupplier : NativeInvokeableClass<Interop.TemporalCoreCustomSlotSupplierCallbacks>
    {
        private readonly ILogger logger;
        private readonly Temporalio.Worker.Tuning.CustomSlotSupplier userSupplier;
        private readonly Dictionary<IntPtr, CancellationTokenSource> pendingReservationTokenSources = new();
        private readonly Dictionary<uint, Temporalio.Worker.Tuning.SlotPermit> permits = new();
        private readonly object mutex = new();
        private uint nextPermitId = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSlotSupplier" /> class.
        /// </summary>
        /// <param name="userSupplier">User's slot supplier implementation'.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        internal unsafe CustomSlotSupplier(
            Temporalio.Worker.Tuning.CustomSlotSupplier userSupplier,
            ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<CustomSlotSupplier>();
            this.userSupplier = userSupplier;

            var interopCallbacks = new Interop.TemporalCoreCustomSlotSupplierCallbacks
            {
                reserve = FunctionPointer<Interop.TemporalCoreCustomSlotSupplierReserveCallback>(Reserve),
                cancel_reserve = FunctionPointer<Interop.TemporalCoreCustomSlotSupplierCancelReserveCallback>(CancelReserve),
                try_reserve = FunctionPointer<Interop.TemporalCoreCustomSlotSupplierTryReserveCallback>(TryReserve),
                mark_used = FunctionPointer<Interop.TemporalCoreCustomSlotSupplierMarkUsedCallback>(MarkUsed),
                release = FunctionPointer<Interop.TemporalCoreCustomSlotSupplierReleaseCallback>(Release),
                free = FunctionPointer<Interop.TemporalCoreCustomSlotSupplierFreeCallback>(Free),
            };

            PinCallbackHolder(interopCallbacks);
        }

        private static Temporalio.Worker.Tuning.SlotInfo SlotInfoFromBridge(Interop.TemporalCoreSlotInfo slotInfo)
        {
            return slotInfo.tag switch
            {
                Interop.TemporalCoreSlotInfo_Tag.WorkflowSlotInfo =>
                    new Temporalio.Worker.Tuning.SlotInfo.WorkflowSlotInfo(
                        ByteArrayRef.ToUtf8(slotInfo.workflow_slot_info.workflow_type), slotInfo.workflow_slot_info.is_sticky != 0),
                Interop.TemporalCoreSlotInfo_Tag.ActivitySlotInfo =>
                    new Temporalio.Worker.Tuning.SlotInfo.ActivitySlotInfo(
                        ByteArrayRef.ToUtf8(slotInfo.activity_slot_info.activity_type)),
                Interop.TemporalCoreSlotInfo_Tag.LocalActivitySlotInfo =>
                    new Temporalio.Worker.Tuning.SlotInfo.LocalActivitySlotInfo(
                        ByteArrayRef.ToUtf8(slotInfo.local_activity_slot_info.activity_type)),
                _ => throw new System.ArgumentOutOfRangeException(nameof(slotInfo)),
            };
        }

        private static unsafe Temporalio.Worker.Tuning.SlotReserveContext ReserveCtxFromBridge(Interop.TemporalCoreSlotReserveCtx* ctx)
        {
            return new(
                SlotType: (*ctx).slot_type switch
                {
                    Interop.TemporalCoreSlotKindType.WorkflowSlotKindType => Temporalio.Worker.Tuning.SlotType.Workflow,
                    Interop.TemporalCoreSlotKindType.ActivitySlotKindType => Temporalio.Worker.Tuning.SlotType.Activity,
                    Interop.TemporalCoreSlotKindType.LocalActivitySlotKindType => Temporalio.Worker.Tuning.SlotType.LocalActivity,
                    _ => throw new System.ArgumentOutOfRangeException(nameof(ctx)),
                },
                TaskQueue: ByteArrayRef.ToUtf8((*ctx).task_queue),
                WorkerIdentity: ByteArrayRef.ToUtf8((*ctx).worker_identity),
                WorkerBuildId: ByteArrayRef.ToUtf8((*ctx).worker_build_id),
                IsSticky: (*ctx).is_sticky != 0);
        }

        private static unsafe Temporalio.Worker.Tuning.SlotReleaseContext ReleaseCtxFromBridge(
            Interop.TemporalCoreSlotReleaseCtx* ctx,
            Temporalio.Worker.Tuning.SlotPermit permit)
        {
            return new(
                SlotInfo: (*ctx).slot_info is null ? null : SlotInfoFromBridge(*(*ctx).slot_info),
                Permit: permit);
        }

        private static unsafe Temporalio.Worker.Tuning.SlotMarkUsedContext MarkUsedCtxFromBridge(
            Interop.TemporalCoreSlotMarkUsedCtx* ctx,
            Temporalio.Worker.Tuning.SlotPermit permit)
        {
            return new(
                SlotInfo: SlotInfoFromBridge((*ctx).slot_info),
                Permit: permit);
        }

        private unsafe void Reserve(Interop.TemporalCoreSlotReserveCtx* ctx, Interop.TemporalCoreSlotReserveCompletionCtx* completionCtx, void* userData)
        {
            SafeReserve(ReserveCtxFromBridge(ctx), new IntPtr(completionCtx));
        }

        private unsafe void CancelReserve(Interop.TemporalCoreSlotReserveCompletionCtx* completionCtx, void* userData)
        {
            CancellationTokenSource? source;
            lock (mutex)
            {
                if (!pendingReservationTokenSources.TryGetValue(new(completionCtx), out source))
                {
                    return;
                }
            }
            source.Cancel();
        }

        private void SafeReserve(Temporalio.Worker.Tuning.SlotReserveContext ctx, IntPtr completionCtx)
        {
            var cancelTokenSrc = new CancellationTokenSource();
            lock (mutex)
            {
                pendingReservationTokenSources.Add(completionCtx, cancelTokenSrc);
            }
            var x = Task.Run(async () =>
            {
                while (true)
                {
                    if (cancelTokenSrc.Token.IsCancellationRequested)
                    {
                        lock (mutex)
                        {
                            pendingReservationTokenSources.Remove(completionCtx);
                            CompleteCancelReserve(completionCtx);
                            return;
                        }
                    }

                    Temporalio.Worker.Tuning.SlotPermit? permit = null;
                    try
                    {
                        permit = await userSupplier.ReserveSlotAsync(ctx, cancelTokenSrc.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancelTokenSrc.Token.IsCancellationRequested)
                    {
                        continue;
                    }
#pragma warning disable CA1031 // We are ok catching all exceptions here
                    catch (Exception e)
                    {
#pragma warning restore CA1031
                        logger.LogError(e, "Error reserving slot");
                    }

                    if (permit != null)
                    {
                        uint usedPermitId;
                        lock (mutex)
                        {
                            pendingReservationTokenSources.Remove(completionCtx);
                            usedPermitId = UnsynchronizedAddPermitToMap(permit);
                        }

                        byte result;
                        unsafe
                        {
                            result = Interop.Methods.temporal_core_complete_async_reserve(
                                (Interop.TemporalCoreSlotReserveCompletionCtx*)completionCtx.ToPointer(),
                                new(usedPermitId));
                        }
                        if (result == 0)
                        {
                            lock (mutex)
                            {
                                permits.Remove(usedPermitId);
                            }
                            CompleteCancelReserve(completionCtx);
                        }

                        return;
                    }
                    else
                    {
                        // Wait for a bit to avoid spamming errors
                        try
                        {
                            await Task.Delay(1000, cancelTokenSrc.Token).ConfigureAwait(false);
                        }
#pragma warning disable CA1031 // We are ok catching all exceptions here
                        catch (Exception)
                        {
#pragma warning restore CA1031
                            // Do nothing, the loop starts by checking cancellation
                        }
                    }
                }
            });
        }

        private unsafe UIntPtr TryReserve(Interop.TemporalCoreSlotReserveCtx* ctx, void* userData)
        {
            Temporalio.Worker.Tuning.SlotPermit? maybePermit;
            try
            {
                maybePermit = userSupplier.TryReserveSlot(ReserveCtxFromBridge(ctx));
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

            lock (mutex)
            {
                return new(UnsynchronizedAddPermitToMap(maybePermit));
            }
        }

        private unsafe void MarkUsed(Interop.TemporalCoreSlotMarkUsedCtx* ctx, void* userData)
        {
            try
            {
                Temporalio.Worker.Tuning.SlotPermit permit;
                lock (mutex)
                {
                    permit = permits[(*ctx).slot_permit.ToUInt32()];
                }
                userSupplier.MarkSlotUsed(MarkUsedCtxFromBridge(ctx, permit));
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error marking slot used");
            }
        }

        private unsafe void Release(Interop.TemporalCoreSlotReleaseCtx* ctx, void* userData)
        {
            var permitId = (*ctx).slot_permit.ToUInt32();
            Temporalio.Worker.Tuning.SlotPermit permit;
            lock (mutex)
            {
                permit = permits[permitId];
            }
            try
            {
                userSupplier.ReleaseSlot(ReleaseCtxFromBridge(ctx, permit));
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error releasing slot");
            }
            finally
            {
                lock (mutex)
                {
                    permits.Remove(permitId);
                }
            }
        }

        // this method must be called under a lock
        private uint UnsynchronizedAddPermitToMap(Temporalio.Worker.Tuning.SlotPermit permit)
        {
            while (true)
            {
                var usedPermitId = nextPermitId;
                nextPermitId = nextPermitId == uint.MaxValue ? 1 : nextPermitId + 1;

                try
                {
                    permits.Add(usedPermitId, permit);
                    return usedPermitId;
                }
                catch (ArgumentException)
                {
                    // ID already in use, try another one
                }
            }
        }

        private void CompleteCancelReserve(IntPtr completionCtx)
        {
            byte result;
            unsafe
            {
                result = Interop.Methods.temporal_core_complete_async_cancel_reserve(
                    (Interop.TemporalCoreSlotReserveCompletionCtx*)completionCtx.ToPointer());
            }
            if (result == 0)
            {
                logger.LogError("Error trying to complete reserve slot cancellation");
                // Nothing we can do here
            }
        }
    }
}
