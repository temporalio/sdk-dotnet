using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<IntPtr, CancellationTokenSource> reservationCancelSources = new();
        private readonly ConcurrentDictionary<IntPtr, Temporalio.Worker.Tuning.SlotPermit> permits = new();

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

        private unsafe void Reserve(Interop.TemporalCoreSlotReserveCtx* ctx, Interop.TemporalCoreSlotReserveCompletionCtx* completionCtx, void* userData)
        {
            SafeReserve(ReserveCtxFromBridge(ctx), new IntPtr(completionCtx));
        }

        private unsafe void CancelReserve(Interop.TemporalCoreSlotReserveCompletionCtx* completionCtx, void* userData)
        {
            if (reservationCancelSources.TryGetValue(new(completionCtx), out var source))
            {
                source.Cancel();
            }
        }

        private void SafeReserve(Temporalio.Worker.Tuning.SlotReserveContext ctx, IntPtr completionCtx)
        {
            var cancelTokenSrc = new CancellationTokenSource();
            if (!reservationCancelSources.TryAdd(completionCtx, cancelTokenSrc))
            {
                logger.LogError("Duplicate slot reservation - CompletionCtx = {CompletionCtx:X}", completionCtx);
                cancelTokenSrc.Dispose();
                return;
            }

            var x = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        try
                        {
                            cancelTokenSrc.Token.ThrowIfCancellationRequested();
                            var permit = await userSupplier.ReserveSlotAsync(ctx, cancelTokenSrc.Token)
                                .ConfigureAwait(false);
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
                                // We need to undo the reservation
                                CompleteCancelReserve(completionCtx);
                                Release(null, permitId);
                            }

                            break;
                        }
                        catch (OperationCanceledException) when (cancelTokenSrc.Token.IsCancellationRequested)
                        {
                            CompleteCancelReserve(completionCtx);
                            break;
                        }
#pragma warning disable CA1031 // We are ok catching all exceptions here
                        catch (Exception e)
                        {
#pragma warning restore CA1031
                            logger.LogError(e, "Error reserving slot");
                        }

                        // Wait for a bit to avoid spamming errors
                        try
                        {
                            await Task.Delay(1000, cancelTokenSrc.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            CompleteCancelReserve(completionCtx);
                            break;
                        }
                    }
                }
#pragma warning disable CA1031 // The task is detached, logging here is the only way to observe exceptions.
                catch (Exception e)
                {
#pragma warning restore CA1031
                    logger.LogError(e, "Exception escaped reserve slot retry loop");
                }
                finally
                {
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
                    return new(StorePermit(permit).ToPointer());
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
            try
            {
                IntPtr permitId = new(ctx->slot_permit.ToPointer());
                if (!permits.TryGetValue(permitId, out var permit))
                {
                    logger.LogError("Error marking slot used: slot permit not found: {PermitId:X}", permitId);
                    return;
                }

                userSupplier.MarkSlotUsed(new(SlotInfoFromBridge(ctx->slot_info), permit));
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
            var permitId = new IntPtr(ctx->slot_permit.ToPointer());
            var slotInfo = ctx->slot_info is null ? null : SlotInfoFromBridge(*ctx->slot_info);
            Release(slotInfo, permitId);
        }

        private void Release(Temporalio.Worker.Tuning.SlotInfo? slotInfo, IntPtr permitId)
        {
            try
            {
                if (!permits.TryRemove(permitId, out var permit))
                {
                    logger.LogError("Error releasing slot: slot permit not found for ID {PermitId:X}", permitId);
                    return;
                }

                GCHandle.FromIntPtr(permitId).Free();
                userSupplier.ReleaseSlot(new(slotInfo, permit));
            }
#pragma warning disable CA1031 // We are ok catching all exceptions here
            catch (Exception e)
            {
#pragma warning restore CA1031
                logger.LogError(e, "Error releasing slot");
            }
        }

        private IntPtr StorePermit(Temporalio.Worker.Tuning.SlotPermit permit)
        {
            // We use an address of a newly created pinned object as a unique permit ID.
            // We cannot pin the permit itself because the type of permit may be unpinnable.
            var permitId = GCHandle.ToIntPtr(GCHandle.Alloc(new byte[1], GCHandleType.Pinned));
            permits[permitId] = permit;
            return permitId;
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
