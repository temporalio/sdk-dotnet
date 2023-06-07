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
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record BackfillScheduleInput(
        string ID,
        IReadOnlyCollection<ScheduleBackfill> Backfills,
        RpcOptions? RpcOptions);
}