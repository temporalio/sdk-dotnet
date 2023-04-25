using Temporalio.Client.Schedules;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.CreateScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="Schedule">Schedule.</param>
    /// <param name="Options">Schedule options.</param>
    public record CreateScheduleInput(
        string ID,
        Schedule Schedule,
        ScheduleOptions? Options);
}