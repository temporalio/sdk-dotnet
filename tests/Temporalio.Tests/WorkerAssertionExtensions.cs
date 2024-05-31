using Temporalio.Api.History.V1;
using Temporalio.Client;
using Xunit;

namespace Temporalio.Tests
{
    public static class WorkerAssertionExtensions
    {
        public static Task AssertTaskFailureEventuallyAsync(
            this WorkflowHandle handle, Action<WorkflowTaskFailedEventAttributes> assert)
        {
            return AssertMore.EventuallyAsync(async () =>
            {
                WorkflowTaskFailedEventAttributes? attrs = null;
                await foreach (var evt in handle.FetchHistoryEventsAsync())
                {
                    if (evt.WorkflowTaskFailedEventAttributes != null)
                    {
                        attrs = evt.WorkflowTaskFailedEventAttributes;
                    }
                }
                Assert.NotNull(attrs);
                assert(attrs!);
            });
        }

        public static Task AssertStartedEventuallyAsync(this WorkflowHandle handle)
        {
            return handle.AssertHasEventEventuallyAsync(e => e.WorkflowExecutionStartedEventAttributes != null);
        }

        public static async Task AssertChildStartedEventuallyAsync(this WorkflowHandle handle)
        {
            // Wait for started
            string? childId = null;
            await AssertHasEventEventuallyAsync(
                handle,
                e =>
                {
                    childId = e.ChildWorkflowExecutionStartedEventAttributes?.WorkflowExecution?.WorkflowId;
                    return childId != null;
                });
            // Check that a workflow task has completed proving child has really started
            await handle.Client.GetWorkflowHandle(childId!).AssertHasEventEventuallyAsync(
                e => e.WorkflowTaskCompletedEventAttributes != null);
        }

        public static Task AssertHasEventEventuallyAsync(
            this WorkflowHandle handle, Func<HistoryEvent, bool> predicate)
        {
            return AssertMore.EventuallyAsync(async () =>
            {
                await foreach (var evt in handle.FetchHistoryEventsAsync())
                {
                    if (predicate(evt))
                    {
                        return;
                    }
                }
                Assert.Fail("Event not found");
            });
        }
    }
}
