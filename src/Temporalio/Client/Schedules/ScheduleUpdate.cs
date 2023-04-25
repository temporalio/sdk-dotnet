namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// An update returned from an updater.
    /// </summary>
    /// <param name="Schedule">Schedule to update.</param>
    public record ScheduleUpdate(Schedule Schedule);
}