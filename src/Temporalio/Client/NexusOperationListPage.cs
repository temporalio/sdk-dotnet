using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Result type of paginated Nexus operation listing.
    /// </summary>
    /// <param name="Operations">A page from the list of operations matching the query.</param>
    /// <param name="NextPageToken">
    /// Token to pass to retrieve the next page. Null if there are no more pages.
    /// </param>
    /// <remarks>WARNING: Standalone Nexus operations are experimental.</remarks>
    public record NexusOperationListPage(IReadOnlyCollection<NexusOperationExecution> Operations, byte[]? NextPageToken);
}
