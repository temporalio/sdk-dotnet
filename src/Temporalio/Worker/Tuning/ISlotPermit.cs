namespace Temporalio.Worker.Tuning
{
    // This interface is intended as a marker type
#pragma warning disable CA1040
    /// <summary>
    /// A permit to use a slot for a workflow/activity/local activity task.
    /// You can implement this interface to add your own data to the permit.
    /// </summary>
    /// <remarks>
    /// WARNING: Custom slot suppliers are currently experimental.
    /// </remarks>
    public interface ISlotPermit
    {
    }
#pragma warning restore CA1040
}
