using Temporalio.Client.Schedules;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CreateScheduleAsync" />.
    /// </summary>
    /// <param name="Id">Schedule ID.</param>
    /// <param name="Schedule">Schedule.</param>
    /// <param name="Options">Schedule options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record CreateScheduleInput(
        string Id,
        Schedule Schedule,
        ScheduleOptions? Options);
}