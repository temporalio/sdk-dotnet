namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Parameter passed to a schedule updater.
    /// </summary>
    /// <param name="Description">Description fetched from the server before this update
    /// call.</param>
    public record ScheduleUpdateInput(
        ScheduleDescription Description);
}