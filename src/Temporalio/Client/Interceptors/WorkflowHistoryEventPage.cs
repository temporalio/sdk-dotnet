using System.Collections.Generic;
using Temporalio.Api.History.V1;

namespace Temporalio.Client.Interceptors
{
    public record WorkflowHistoryEventPage(
        IReadOnlyCollection<HistoryEvent> Events,
        byte[]? NextPageToken
    );
}