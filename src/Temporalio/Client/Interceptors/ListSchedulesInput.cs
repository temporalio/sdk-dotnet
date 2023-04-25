#if NETCOREAPP3_0_OR_GREATER
using Temporalio.Client.Schedules;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.ListSchedulesAsync" />.
    /// </summary>
    /// <param name="Options">List options.</param>
    public record ListSchedulesInput(
        ScheduleListOptions? Options);
}
#endif