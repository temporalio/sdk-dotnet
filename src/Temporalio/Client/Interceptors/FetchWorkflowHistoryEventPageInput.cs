using Temporalio.Api.Enums.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.FetchWorkflowHistoryEventPage" />.
    /// </summary>
    /// <param name="ID">ID of the workflow.</param>
    /// <param name="RunID">Optional run ID of the workflow.</param>
    /// <param name="PageSize">Optional page size.</param>
    /// <param name="NextPageToken">Next page token if any to continue pagination.</param>
    /// <param name="WaitNewEvent">If true, wait for new events before returning.</param>
    /// <param name="EventFilterType">Type of events to return.</param>
    /// <param name="SkipArchival">If true, skips archival when fetching.</param>
    /// <param name="Rpc">RPC options.</param>
    public record FetchWorkflowHistoryEventPageInput(
        string ID,
        string? RunID,
        int? PageSize,
        byte[]? NextPageToken,
        bool WaitNewEvent,
        HistoryEventFilterType EventFilterType,
        bool SkipArchival,
        RpcOptions? Rpc);
}