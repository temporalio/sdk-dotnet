using System.Collections.Generic;
using Temporalio.Client.Schedules;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.BackfillScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="Backfills">Backfills.</param>
    /// <param name="RpcOptions">RPC options.</param>
    public record BackfillScheduleInput(
        string ID,
        IReadOnlyCollection<ScheduleBackfill> Backfills,
        RpcOptions? RpcOptions);
}