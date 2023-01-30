using System.Collections.Generic;
using Temporalio.Api.History.V1;

namespace Temporalio
{
    /// <summary>
    /// History for a workflow.
    /// </summary>
    /// <param name="ID">ID of the workflow.</param>
    /// <param name="Events">Collection of events.</param>
    public record WorkflowHistory(string ID, IReadOnlyCollection<HistoryEvent> Events);
}