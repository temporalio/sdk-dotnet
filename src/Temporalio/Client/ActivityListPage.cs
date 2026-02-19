using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Result type of paginated activity listing.
    /// </summary>
    /// <param name="Activities">A page from the list of activities matching the query.</param>
    /// <param name="NextPageToken">
    /// Token to pass to retrieve the next page. Null if there are no more pages.
    /// </param>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public record ActivityListPage(IReadOnlyCollection<ActivityExecution> Activities, byte[]? NextPageToken);
}
