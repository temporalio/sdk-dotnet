using System.Collections.Generic;
using Temporalio.Api.History.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Page of workflow history events.
    /// </summary>
    /// <param name="Events">Collection of events.</param>
    /// <param name="NextPageToken">Token for getting the next page if any.</param>
    public record WorkflowHistoryEventPage(
        IReadOnlyCollection<HistoryEvent> Events,
        byte[]? NextPageToken);
}