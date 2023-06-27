using System;
using System.Threading.Tasks;
using Temporalio.Client.Schedules;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.UpdateScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="Updater">Updater.</param>
    /// <param name="RpcOptions">RPC options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record UpdateScheduleInput(
        string ID,
        Func<ScheduleUpdateInput, Task<ScheduleUpdate?>> Updater,
        RpcOptions? RpcOptions);
}