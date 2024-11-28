using System.Runtime.InteropServices;

namespace Temporalio.Worker.Tuning
{
    // This class actually _is_ meant to be inherited with no members.
#pragma warning disable CA1052
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
#pragma warning restore CA1052
}
