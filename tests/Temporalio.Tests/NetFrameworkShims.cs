#if NETFRAMEWORK
namespace Temporalio.Tests;

using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Client;
using Temporalio.Common;

// The SDK omits its IAsyncEnumerable-based APIs on .NET Framework. Tests use these only to make
// assertions about other behavior, so these stand-ins let those tests still run there.
public static class NetFrameworkShims
{
    public static async Task<WorkflowHistory> FetchHistoryAsync(this WorkflowHandle handle)
    {
        var events = new List<HistoryEvent>();
        await foreach (var evt in handle.FetchHistoryEventsAsync())
        {
            events.Add(evt);
        }
        return new(handle.Id, events);
    }

    public static async IAsyncEnumerable<HistoryEvent> FetchHistoryEventsAsync(
        this WorkflowHandle handle)
    {
        byte[]? nextPageToken = null;
        do
        {
            var page = await handle.Client.OutboundInterceptor.FetchWorkflowHistoryEventPageAsync(new(
                Id: handle.Id,
                RunId: handle.RunId,
                PageSize: 0,
                NextPageToken: nextPageToken,
                WaitNewEvent: false,
                EventFilterType: HistoryEventFilterType.AllEvent,
                SkipArchival: false,
                Rpc: null));
            foreach (var evt in page.Events)
            {
                yield return evt;
            }
            nextPageToken = page.NextPageToken;
        }
        while (nextPageToken != null);
    }
}
#endif
