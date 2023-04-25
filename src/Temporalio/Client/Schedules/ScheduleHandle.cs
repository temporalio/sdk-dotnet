using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Handle for interacting with a schedule.
    /// </summary>
    /// <param name="Client">Client used for schedule handle calls.</param>
    /// <param name="ID">Schedule ID.</param>
    public record ScheduleHandle(
        ITemporalClient Client,
        string ID)
    {
        /// <summary>
        /// Backfill this schedule by going through the specified time periods as if they passed
        /// right now.
        /// </summary>
        /// <param name="backfills">Backfill periods.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Task for completion.</returns>
        public Task BackfillAsync(
            IReadOnlyCollection<ScheduleBackfill> backfills, RpcOptions? rpcOptions = null)
        {
            if (backfills.Count == 0)
            {
                throw new ArgumentException("At least one backfill required");
            }
            return Client.OutboundInterceptor.BackfillScheduleAsync(new(
                ID: ID, Backfills: backfills, RpcOptions: rpcOptions));
        }

        /// <summary>
        /// Delete this schedule.
        /// </summary>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Task for completion.</returns>
        public Task DeleteAsync(RpcOptions? rpcOptions = null) =>
            Client.OutboundInterceptor.DeleteScheduleAsync(new(ID: ID, RpcOptions: rpcOptions));

        /// <summary>
        /// Fetch this schedule's description.
        /// </summary>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Schedule description.</returns>
        public Task<ScheduleDescription> DescribeAsync(RpcOptions? rpcOptions = null) =>
            Client.OutboundInterceptor.DescribeScheduleAsync(new(ID: ID, RpcOptions: rpcOptions));

        /// <summary>
        /// Pause this schedule.
        /// </summary>
        /// <param name="note">Note to set when pausing.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Task for completion.</returns>
        public Task PauseAsync(string? note = null, RpcOptions? rpcOptions = null) =>
            Client.OutboundInterceptor.PauseScheduleAsync(
                new(ID: ID, Note: note, RpcOptions: rpcOptions));

        /// <summary>
        /// Trigger an action on this schedule to happen immediately.
        /// </summary>
        /// <param name="options">Options for triggering.</param>
        /// <returns>Task for completion.</returns>
        public Task TriggerAsync(ScheduleTriggerOptions? options = null) =>
            Client.OutboundInterceptor.TriggerScheduleAsync(new(ID: ID, Options: options));

        /// <summary>
        /// Unpause this schedule.
        /// </summary>
        /// <param name="note">Note to set when unpausing.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Task for completion.</returns>
        public Task UnpauseAsync(string? note = null, RpcOptions? rpcOptions = null) =>
            Client.OutboundInterceptor.UnpauseScheduleAsync(
                new(ID: ID, Note: note, RpcOptions: rpcOptions));

        /// <summary>
        /// Update this schedule. This is done via a callback which can be called multiple times in
        /// case of conflict.
        /// </summary>
        /// <param name="updater">Callback to invoke with the current update input. The result can
        /// be null to signify no update to perform, or a schedule update instance with a schedule
        /// to perform an update.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Task for completion.</returns>
        public Task UpdateAsync(
            Func<ScheduleUpdateInput, ScheduleUpdate?> updater,
            RpcOptions? rpcOptions = null) =>
            UpdateAsync(input => Task.FromResult(updater(input)), rpcOptions);

        /// <summary>
        /// Update this schedule. This is done via a callback which can be called multiple times in
        /// case of conflict.
        /// </summary>
        /// <param name="updater">Callback to invoke with the current update input. The result can
        /// be null to signify no update to perform, or a schedule update instance with a schedule
        /// to perform an update.</param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Task for completion.</returns>
        public Task UpdateAsync(
            Func<ScheduleUpdateInput, Task<ScheduleUpdate?>> updater,
            RpcOptions? rpcOptions = null) =>
            Client.OutboundInterceptor.UpdateScheduleAsync(
                new(ID: ID, Updater: updater, RpcOptions: rpcOptions));
    }
}