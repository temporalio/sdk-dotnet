using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Temporalio.Worker.Tuning;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Core wrapper for a user-defined custom slot supplier.
    /// </summary>
    internal class CustomSlotSupplier : NativeInvokeableClass<Interop.TemporalCoreCustomSlotSupplierCallbacks>
    {
        private readonly ILogger logger;
        private readonly Temporalio.Worker.Tuning.CustomSlotSupplier userSupplier;
        private readonly ConcurrentDictionary<IntPtr, CancellationTokenSource> reservationCancelSources = new();
        private readonly ConcurrentDictionary<IntPtr, SlotPermit> permits = new();

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
                available_slots = IntPtr.Zero,
                free = FunctionPointer<Interop.TemporalCoreCustomSlotSupplierFreeCallback>(Free),
                user_data = null,
            };

            PinCallbackHolder(interopCallbacks);
        }

        private static SlotInfo SlotInfoFromBridge(Interop.TemporalCoreSlotInfo slotInfo)
        {
            return slotInfo.tag switch
            {
                Interop.TemporalCoreSlotInfo_Tag.WorkflowSlotInfo =>
                    new SlotInfo.WorkflowSlotInfo(
                        ByteArrayRef.ToUtf8(slotInfo.workflow_slot_info.workflow_type), slotInfo.workflow_slot_info.is_sticky != 0),
                Interop.TemporalCoreSlotInfo_Tag.ActivitySlotInfo =>
                    new SlotInfo.ActivitySlotInfo(
                        ByteArrayRef.ToUtf8(slotInfo.activity_slot_info.activity_type)),
                Interop.TemporalCoreSlotInfo_Tag.LocalActivitySlotInfo =>
                    new SlotInfo.LocalActivitySlotInfo(
                        ByteArrayRef.ToUtf8(slotInfo.local_activity_slot_info.activity_type)),
                Interop.TemporalCoreSlotInfo_Tag.NexusSlotInfo =>
                    new SlotInfo.NexusOperationSlotInfo(
                        ByteArrayRef.ToUtf8(slotInfo.nexus_slot_info.service),
                        ByteArrayRef.ToUtf8(slotInfo.nexus_slot_info.operation)),
                _ => throw new ArgumentOutOfRangeException(nameof(slotInfo)),
            };
        }

        private static unsafe SlotReserveContext ReserveCtxFromBridge(Interop.TemporalCoreSlotReserveCtx* ctx)
        {
            return new(
                SlotType: (*ctx).slot_type switch
                {
                    Interop.TemporalCoreSlotKindType.WorkflowSlotKindType => SlotType.Workflow,
                    Interop.TemporalCoreSlotKindType.ActivitySlotKindType => SlotType.Activity,
                    Interop.TemporalCoreSlotKindType.LocalActivitySlotKindType => SlotType.LocalActivity,
                    Interop.TemporalCoreSlotKindType.NexusSlotKindType => SlotType.NexusOperation,
                    _ => throw new ArgumentOutOfRangeException(nameof(ctx)),
                },
                TaskQueue: ByteArrayRef.ToUtf8((*ctx).task_queue),
                WorkerIdentity: ByteArrayRef.ToUtf8((*ctx).worker_identity),
                WorkerBuildId: ByteArrayRef.ToUtf8((*ctx).worker_build_id),
                IsSticky: (*ctx).is_sticky != 0);
        }

        private unsafe void Reserve(Interop.TemporalCoreSlotReserveCtx* ctx, Interop.TemporalCoreSlotReserveCompletionCtx* completionCtx, void* userData)
        {
            SafeReserve(ReserveCtxFromBridge(ctx), new IntPtr(completionCtx));
        }

        private unsafe void CancelReserve(Interop.TemporalCoreSlotReserveCompletionCtx* completionCtx, void* userData)
        {
            // Failed lookup is OK because cancellation is checked by Core too.
            if (reservationCancelSources.TryGetValue(new(completionCtx), out var source))
            {
                try
                {
                    source.Cancel();
                }
#pragma warning disable CA1031 // We are ok catching all exceptions here
                catch (Exception)
                {
                }
#pragma warning restore CA1031
            }
        }

        private void SafeReserve(SlotReserveContext ctx, IntPtr completionCtx)
        {
            var cancelTokenSrc = new CancellationTokenSource();
            // Invariant: if `completionCtx` is present in `reservationCancelSources`, then neither
            // `complete_async_reserve` nor `complete_async_cancel_reserve` have been called yet.
            if (!reservationCancelSources.TryAdd(completionCtx, cancelTokenSrc))
            {
                logger.LogError("Duplicate slot reservation - CompletionCtx = {CompletionCtx:X}", completionCtx);
                cancelTokenSrc.Dispose();
                return;
            }

            logger.LogDebug("Slot reservation started - CompletionCtx = {CompletionCtx:X}", completionCtx);
            var x = Task.Run(async () =>
            {
                try
                {
                    SlotPermit permit;
                    while (true)
                    {
                        try
                        {
                            cancelTokenSrc.Token.ThrowIfCancellationRequested();
                            permit = await userSupplier.ReserveSlotAsync(ctx, cancelTokenSrc.Token)
                                .ConfigureAwait(false);
                            break;
                        }
                        catch (OperationCanceledException) when (cancelTokenSrc.Token.IsCancellationRequested)
                        {
                            CompleteCancelReserve(completionCtx);
                            return;
                        }
#pragma warning disable CA1031 // We are ok catching all exceptions here
                        catch (Exception e)
                        {
#pragma warning restore CA1031
                            logger.LogError(e, "Error reserving slot - CompletionCtx = {CompletionCtx:X}", completionCtx);
                            // Wait for a bit to avoid spamming errors
                            try
                            {
                                await Task.Delay(1000, cancelTokenSrc.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                CompleteCancelReserve(completionCtx);
                                return;
                            }
                        }
                    }

                    // There should be no exceptions possible from here onward.
                    //
                    // User handler completed, so we don't need the cancellation source anymore. It needs to be removed
                    // from the map before calling `complete_async_reserve` to avoid race condition. Final cancellation
                    // check is done inside `complete_async_reserve`.
                    reservationCancelSources.TryRemove(completionCtx, out _);
                    var permitId = StorePermit(permit);

                    byte result;
                    unsafe
                    {
                        result = Interop.Methods.temporal_core_complete_async_reserve(
                            (Interop.TemporalCoreSlotReserveCompletionCtx*)completionCtx.ToPointer(),
                            new(permitId.ToPointer()));
                    }

                    if (result == 0)
                    {
                        CompleteCancelReserve(completionCtx);
                        // We need to undo the reservation
                        Release(null, permitId);
                    }
                    else
                    {
                        logger.LogDebug("Slot reservation completed - CompletionCtx = {CompletionCtx:X}, PermitId = {PermitId:X}", completionCtx, permitId);
                    }
                }
#pragma warning disable CA1031 // The task is detached, logging here is the only way to observe exceptions.
                catch (Exception e)
                {
#pragma warning restore CA1031
                    logger.LogError(e, "Exception happened outside of retry loop when reserving slot - CompletionCtx = {CompletionCtx:X}", completionCtx);
                }
                finally
                {
                    // Normally the cancellation source will be already removed by this point, but it may still be
                    // present if there was an exception.
                    reservationCancelSources.TryRemove(completionCtx, out _);
                    cancelTokenSrc.Dispose();
                }
            });
        }

        private unsafe UIntPtr TryReserve(Interop.TemporalCoreSlotReserveCtx* ctx, void* userData)
        {
            try
            {
                var permit = userSupplier.TryReserveSlot(ReserveCtxFromBridge(ctx));
                if (permit != null)
                {
                    var permitId = StorePermit(permit);
                    logger.LogDebug("Slot reservation completed (TryReserve) - PermitId = {PermitId:X}", permitId);
                    return new(permitId.ToPointer());
                }
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error trying to reserve slot");
            }

            return UIntPtr.Zero;
        }

        private unsafe void MarkUsed(Interop.TemporalCoreSlotMarkUsedCtx* ctx, void* userData)
        {
            IntPtr permitId = new(ctx->slot_permit.ToPointer());
            try
            {
                if (!permits.TryGetValue(permitId, out var permit))
                {
                    logger.LogError("Error marking slot used: slot permit not found - PermitId = {PermitId:X}", permitId);
                    return;
                }

                userSupplier.MarkSlotUsed(new(SlotInfoFromBridge(ctx->slot_info), permit));
                logger.LogDebug("Slot marked used - PermitId = {PermitId:X}", permitId);
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error marking slot used - PermitId = {PermitId:X}", permitId);
            }
        }

        private unsafe void Release(Interop.TemporalCoreSlotReleaseCtx* ctx, void* userData)
        {
            var permitId = new IntPtr(ctx->slot_permit.ToPointer());
            var slotInfo = ctx->slot_info is null ? null : SlotInfoFromBridge(*ctx->slot_info);
            Release(slotInfo, permitId);
        }

        private void Release(SlotInfo? slotInfo, IntPtr permitId)
        {
            try
            {
                if (!permits.TryRemove(permitId, out var permit))
                {
                    logger.LogError("Error releasing slot: slot permit not found - PermitId = {PermitId:X}", permitId);
                    return;
                }

                Marshal.FreeHGlobal(permitId);
                userSupplier.ReleaseSlot(new(slotInfo, permit));
                logger.LogDebug("Slot released - PermitId = {PermitId:X}", permitId);
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error releasing slot - PermitId = {PermitId:X}", permitId);
            }
        }

        private IntPtr StorePermit(Temporalio.Worker.Tuning.SlotPermit permit)
        {
            // We use an address of a fresh allocation as a unique permit ID.
            // We cannot use the address of the permit itself because the type of permit may be unpinnable.
            var permitId = Marshal.AllocHGlobal(1);
            permits[permitId] = permit;
            return permitId;
        }

        private void CompleteCancelReserve(IntPtr completionCtx)
        {
            logger.LogDebug("Slot reservation cancelled - CompletionCtx = {CompletionCtx:X}", completionCtx);
            reservationCancelSources.TryRemove(completionCtx, out _);

            byte result;
            unsafe
            {
                result = Interop.Methods.temporal_core_complete_async_cancel_reserve(
                    (Interop.TemporalCoreSlotReserveCompletionCtx*)completionCtx.ToPointer());
            }
            if (result == 0)
            {
                logger.LogError("Error trying to complete reserve slot cancellation - CompletionCtx = {CompletionCtx:X}", completionCtx);
                // Nothing we can do here
            }
        }
    }
}
