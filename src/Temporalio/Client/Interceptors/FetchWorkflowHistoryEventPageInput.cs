
using System;
using System.Collections.Generic;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;

namespace Temporalio.Client.Interceptors
{
    public record FetchWorkflowHistoryEventPageInput(
        string ID,
        string? RunID,
        int? PageSize,
        byte[]? NextPageToken,
        bool WaitNewEvent,
        HistoryEventFilterType EventFilterType,
        bool SkipArchival,
        RpcOptions? Rpc
    );
}