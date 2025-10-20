using System.Collections.Generic;

namespace Temporalio.Client
{
    /// <summary>
    /// Result type of <see cref="ITemporalClient.GetListWorkflowsPageAsync"/>.
    /// </summary>
    /// <param name="Workflows">A page of the list of workflows matching the query.</param>
    /// <param name="NextPageToken">
    /// Token to pass to <see cref="ITemporalClient.GetListWorkflowsPageAsync"/> to retrieve the next page.
    /// Null if it's the last page.
    /// </param>
    public record ListWorkflowsPage(IReadOnlyCollection<WorkflowExecution> Workflows, byte[]? NextPageToken);
}
