namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// A permit to use a slot for a workflow/activity/local activity task.
    /// You can inherit from this class to add your own data to the permit.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public class SlotPermit
    {
    }
}
