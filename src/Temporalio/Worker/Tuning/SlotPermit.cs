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
        private SlotPermit()
        {
        }

        /// <summary>
        /// Reconstruct a permit from a pointer.
        /// </summary>
        /// <param name="permit">The pointer to the permit.</param>
        /// <returns>The permit.</returns>
        internal static unsafe SlotPermit FromPointer(void* permit)
        {
            return (SlotPermit)GCHandle.FromIntPtr(new(permit)).Target!;
        }
    }
#pragma warning restore CA1052
}
