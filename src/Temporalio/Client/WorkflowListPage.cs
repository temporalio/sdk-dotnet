using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Result type of <see cref="ITemporalClient.ListWorkflowsPaginatedAsync"/>.
    /// </summary>
    /// <param name="Workflows">A page from the list of workflows matching the query.</param>
    /// <param name="NextPageToken">
    /// Token to pass to <see cref="ITemporalClient.ListWorkflowsPaginatedAsync"/> to retrieve the next page.
    /// Null if there are no more pages.
    /// </param>
    public record WorkflowListPage(IReadOnlyCollection<WorkflowExecution> Workflows, byte[]? NextPageToken);
}
