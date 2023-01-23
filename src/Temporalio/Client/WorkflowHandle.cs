using System;
using System.Threading.Tasks;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Failure.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
using Temporalio.Api.History.V1;
#endif

namespace Temporalio.Client
{
    public record WorkflowHandle(
        ITemporalClient Client,
        string ID,
        string? RunID = null,
        string? ResultRunID = null,
        string? FirstExecutionRunID = null
    )
    {
        public async Task GetResultAsync(
            bool followRuns = true, RpcOptions? rpcOptions = null)
        {
            await GetResultAsync<ValueTuple>(followRuns, rpcOptions);
        }

        public async Task<TResult> GetResultAsync<TResult>(
            bool followRuns = true, RpcOptions? rpcOptions = null)
        {
            // Continually get pages
            var histRunID = ResultRunID;
            while (true) {
                var page = await Client.OutboundInterceptor.FetchWorkflowHistoryEventPage(new(
                    ID: ID,
                    RunID: histRunID,
                    PageSize: 0,
                    NextPageToken: null,
                    WaitNewEvent: true,
                    EventFilterType: HistoryEventFilterType.CloseEvent,
                    SkipArchival: true,
                    Rpc: TemporalClient.DefaultRetryOptions(rpcOptions)));
                if (page.Events.Count == 0) {
                    throw new InvalidOperationException("Event set unexpectedly empty");
                }
                histRunID = null;
                foreach (var evt in page.Events) {
                    switch (evt.AttributesCase) {
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionCompletedEventAttributes:
                            var compAttr = evt.WorkflowExecutionCompletedEventAttributes;
                            if (compAttr.NewExecutionRunId != string.Empty && followRuns) {
                                histRunID = compAttr.NewExecutionRunId;
                                break;
                            }
                            // Ignore return if they didn't want it
                            if (typeof(TResult) == typeof(ValueTuple)) {
                                return default!;
                            }
                            // Otherwise we expect a single payload
                            if (compAttr.Result == null) {
                                throw new InvalidOperationException("No result present");
                            }
                            return await Client.DataConverter.ToSingleValueAsync<TResult>(
                                compAttr.Result.Payloads_);
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionFailedEventAttributes:
                            var failAttr = evt.WorkflowExecutionFailedEventAttributes;
                            if (failAttr.NewExecutionRunId != string.Empty && followRuns) {
                                histRunID = failAttr.NewExecutionRunId;
                                break;
                            }
                            throw new WorkflowFailureException(
                                await Client.DataConverter.ToExceptionAsync(failAttr.Failure));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionCanceledEventAttributes:
                            var cancelAttr = evt.WorkflowExecutionCanceledEventAttributes;
                            throw new WorkflowFailureException(new CancelledFailureException(
                                new() {
                                    Message = "Workflow cancelled",
                                    CanceledFailureInfo = new() { Details = cancelAttr.Details },
                                }, null, Client.DataConverter.PayloadConverter));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionTerminatedEventAttributes:
                            var termAttr = evt.WorkflowExecutionTerminatedEventAttributes;
                            var reason = termAttr.Reason == string.Empty ? "Workflow terminated" : termAttr.Reason;
                            throw new WorkflowFailureException(new TerminatedFailureException(
                                new() { Message = "Workflow terminated", TerminatedFailureInfo = new() }, null));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionTimedOutEventAttributes:
                            var timeAttr = evt.WorkflowExecutionTimedOutEventAttributes;
                            if (timeAttr.NewExecutionRunId != string.Empty && followRuns) {
                                histRunID = timeAttr.NewExecutionRunId;
                                break;
                            }
                            throw new WorkflowFailureException(new TimeoutFailureException(
                                new()
                                {
                                    Message = "Workflow timed out",
                                    TimeoutFailureInfo = new() { TimeoutType = TimeoutType.StartToClose }
                                }, null, Client.DataConverter.PayloadConverter));
                    }
                }
                // If we didn't get a new ID to follow, we didn't get a completion event
                if (histRunID == null) {
                    throw new InvalidOperationException("No completion event found");
                }
            }
        }

        public async Task SignalAsync(string signal, object[] args, RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public async Task SignalAsync(Func<Task> signal, RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public async Task SignalAsync<T>(Func<T, Task> signal, T arg, RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public async Task<TQueryResult> QueryAsync<TQueryResult>(
        string query, object[] args, RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public async Task<TQueryResult> QueryAsync<TQueryResult>(
        Func<TQueryResult> query, RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public async Task<TQueryResult> QueryAsync<T, TQueryResult>(
        Func<T, TQueryResult> query, T arg, RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public async Task CancelAsync(RpcOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public async Task TerminateAsync(
            string? reason = null, object[]? args = null, RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public async Task<WorkflowExecutionDescription> DescribeAsync(RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        #if NETCOREAPP3_0_OR_GREATER

        public async Task<WorkflowHistory> FetchHistoryAsync(
            HistoryEventFilterType eventFilterType = HistoryEventFilterType.AllEvent,
            bool skipArchival = false,
            RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerator<HistoryEvent> FetchHistoryEvents(
            bool waitNewEvent = false,
            HistoryEventFilterType eventFilterType = HistoryEventFilterType.AllEvent,
            bool skipArchival = false,
            RpcOptions? rpcOptions = null)
        {
            throw new NotImplementedException();
        }

        #endif
    }

    public record WorkflowHandle<TResult>(
        ITemporalClient Client,
        string ID,
        string? RunID = null,
        string? ResultRunID = null,
        string? FirstExecutionRunID = null
    ) : WorkflowHandle(Client, ID, RunID, ResultRunID, FirstExecutionRunID)
    {
        public new Task<TResult> GetResultAsync(
            bool followRuns = true, RpcOptions? rpcOptions = null)
        {
            return GetResultAsync<TResult>(followRuns, rpcOptions);
        }
    }
}