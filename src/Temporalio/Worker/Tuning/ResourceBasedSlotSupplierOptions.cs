using System;

namespace Temporalio.Worker.Tuning
{
    /// <summary>
    /// Options for a specific slot type being used with a resource based slot supplier.
    /// </summary>
    /// <param name="MinimumSlots">Amount of slots that will be issued regardless of any other
    /// checks. Defaults to 5 for workflows and 1 for activities.</param>
    /// <param name="MaximumSlots">Maximum amount of slots permitted. Defaults to 500.</param>
    /// <param name="RampThrottle">
    /// Minimum time we will wait (after passing the minimum slots number) between handing out new
    /// slots in milliseconds. Defaults to 0 for workflows and 50ms for activities.
    ///
    /// This value matters because how many resources a task will use cannot be determined ahead of
    /// time, and thus the system should wait to see how much resources are used before issuing more
    /// slots.
    /// </param>
    /// <remarks>
    /// WARNING: Resource based tuning is currently experimental.
    /// </remarks>
    public sealed record ResourceBasedSlotSupplierOptions(
        int? MinimumSlots = null,
        int? MaximumSlots = null,
        TimeSpan? RampThrottle = null);
}