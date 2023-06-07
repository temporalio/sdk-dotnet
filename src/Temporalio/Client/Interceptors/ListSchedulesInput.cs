#if NETCOREAPP3_0_OR_GREATER
using Temporalio.Client.Schedules;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.ListSchedulesAsync" />.
    /// </summary>
    /// <param name="Options">List options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record ListSchedulesInput(
        ScheduleListOptions? Options);
}
#endif