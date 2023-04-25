using Temporalio.Client.Schedules;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.TriggerScheduleAsync" />.
    /// </summary>
    /// <param name="ID">Schedule ID.</param>
    /// <param name="Options">Trigger options.</param>
    public record TriggerScheduleInput(
        string ID,
        ScheduleTriggerOptions? Options);
}