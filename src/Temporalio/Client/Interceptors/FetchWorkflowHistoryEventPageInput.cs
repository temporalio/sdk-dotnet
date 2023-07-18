using Temporalio.Api.Enums.V1;

namespace Temporalio.Client.Interceptors
{
    /// <summary>
    /// Input for <see cref="ClientOutboundInterceptor.FetchWorkflowHistoryEventPageAsync" />.
    /// </summary>
    /// <param name="Id">ID of the workflow.</param>
    /// <param name="RunId">Optional run ID of the workflow.</param>
    /// <param name="PageSize">Optional page size.</param>
    /// <param name="NextPageToken">Next page token if any to continue pagination.</param>
    /// <param name="WaitNewEvent">If true, wait for new events before returning.</param>
    /// <param name="EventFilterType">Type of events to return.</param>
    /// <param name="SkipArchival">If true, skips archival when fetching.</param>
    /// <param name="Rpc">RPC options.</param>
    /// <remarks>
    /// WARNING: This constructor may have required properties added. Do not rely on the exact
    /// constructor, only use "with" clauses.
    /// </remarks>
    public record FetchWorkflowHistoryEventPageInput(
        string Id,
        string? RunId,
        int? PageSize,
        byte[]? NextPageToken,
        bool WaitNewEvent,
        HistoryEventFilterType EventFilterType,
        bool SkipArchival,
        RpcOptions? Rpc);
}