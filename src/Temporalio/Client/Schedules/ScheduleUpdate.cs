using Temporalio.Common;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// An update returned from an updater.
    /// </summary>
    /// <param name="Schedule">Schedule to update.</param>
    /// <param name="SearchAttributes">Optional indexed attributes that can be used while querying
    /// schedules via the list schedules APIs. The key and value type must be registered with
    /// Temporal server.
    ///
    /// <ul>
    /// <li>If null, the search attributes will not be updated.</li>
    /// <li>If present but empty, the search attributes will be cleared.</li>
    /// <li>If present and non-empty, all search attributes will be updated to the provided
    ///     collection. This means existing attributes which do not exist in the provided collection
    ///     will be wiped out. You can copy attributes from the existing <see
    ///     cref="ScheduleDescription"/> to avoid this.</li>
    /// </ul>
    /// </param>
    public record ScheduleUpdate(
        Schedule Schedule,
        SearchAttributeCollection? SearchAttributes = null);
}