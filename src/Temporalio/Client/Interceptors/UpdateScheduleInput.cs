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
    public record UpdateScheduleInput(
        string ID,
        Func<ScheduleUpdateInput, Task<ScheduleUpdate?>> Updater,
        RpcOptions? RpcOptions);
}