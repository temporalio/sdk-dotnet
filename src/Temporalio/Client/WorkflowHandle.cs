using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Converters;
using Temporalio.Exceptions;

namespace Temporalio.Client
{
    /// <summary>
    /// Workflow handle to perform actions on an individual workflow.
    /// </summary>
    /// <param name="Client">Client used for workflow handle calls.</param>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">
    /// Run ID used for signals and queries if present to ensure a very specific run to call. This
    /// is only set when getting a workflow handle, not when starting a workflow.
    /// </param>
    /// <param name="ResultRunID">
    /// Run ID used for get result calls to ensure getting a result starting from this run. This is
    /// set the same as a run ID when getting a workflow handle. When starting a workflow, this is
    /// set as the resulting run ID.
    /// </param>
    /// <param name="FirstExecutionRunID">
    /// Run ID used for cancellation and termination to ensure they happen on a workflow starting
    /// with this run ID. This can be set when getting a workflow handle. When starting a workflow,
    /// this is set as the resulting run ID if no start signal was provided.
    /// </param>
    public record WorkflowHandle(
        ITemporalClient Client,
        string ID,
        string? RunID = null,
        string? ResultRunID = null,
        string? FirstExecutionRunID = null)
    {
        /// <summary>
        /// Get the result of a workflow disregarding its return (or not having a return type).
        /// </summary>
        /// <param name="followRuns">
        /// Whether to follow runs until the latest workflow is reached.
        /// </param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Untyped task for waiting on result.</returns>
        /// <exception cref="WorkflowFailureException">
        /// Exception thrown for unsuccessful workflow result. The cause can be
        /// <see cref="CancelledFailureException" />, <see cref="TerminatedFailureException" />,
        /// <see cref="TimeoutFailureException" />, or any exception deserialized that was thrown in
        /// the workflow (usually an <see cref="ApplicationFailureException" />).
        /// </exception>
        public async Task GetResultAsync(
            bool followRuns = true, RpcOptions? rpcOptions = null)
        {
            await GetResultAsync<ValueTuple>(followRuns, rpcOptions);
        }

        /// <summary>
        /// Get the result of a workflow, deserializing into the given return type.
        /// </summary>
        /// <typeparam name="TResult">Return type to deserialize result into.</typeparam>
        /// <param name="followRuns">
        /// Whether to follow runs until the latest workflow is reached.
        /// </param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Untyped task for waiting on result.</returns>
        /// <exception cref="WorkflowFailureException">
        /// Exception thrown for unsuccessful workflow result. The cause can be
        /// <see cref="CancelledFailureException" />, <see cref="TerminatedFailureException" />,
        /// <see cref="TimeoutFailureException" />, or any exception deserialized that was thrown in
        /// the workflow (usually an <see cref="ApplicationFailureException" />).
        /// </exception>
        public async Task<TResult> GetResultAsync<TResult>(
            bool followRuns = true, RpcOptions? rpcOptions = null)
        {
            // Continually get pages
            var histRunID = ResultRunID;
            while (true)
            {
                var page = await Client.OutboundInterceptor.FetchWorkflowHistoryEventPage(new(
                    ID: ID,
                    RunID: histRunID,
                    PageSize: 0,
                    NextPageToken: null,
                    WaitNewEvent: true,
                    EventFilterType: HistoryEventFilterType.CloseEvent,
                    SkipArchival: true,
                    Rpc: TemporalClient.DefaultRetryOptions(rpcOptions)));
                if (page.Events.Count == 0)
                {
                    throw new InvalidOperationException("Event set unexpectedly empty");
                }
                histRunID = null;
                foreach (var evt in page.Events)
                {
                    switch (evt.AttributesCase)
                    {
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionCompletedEventAttributes:
                            var compAttr = evt.WorkflowExecutionCompletedEventAttributes;
                            if (compAttr.NewExecutionRunId != string.Empty && followRuns)
                            {
                                histRunID = compAttr.NewExecutionRunId;
                                break;
                            }
                            // Ignore return if they didn't want it
                            if (typeof(TResult) == typeof(ValueTuple))
                            {
                                return default!;
                            }
                            // Otherwise we expect a single payload
                            if (compAttr.Result == null)
                            {
                                throw new InvalidOperationException("No result present");
                            }
                            return await Client.Options.DataConverter.ToSingleValueAsync<TResult>(
                                compAttr.Result.Payloads_);
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionFailedEventAttributes:
                            var failAttr = evt.WorkflowExecutionFailedEventAttributes;
                            if (failAttr.NewExecutionRunId != string.Empty && followRuns)
                            {
                                histRunID = failAttr.NewExecutionRunId;
                                break;
                            }
                            throw new WorkflowFailureException(
                                await Client.Options.DataConverter.ToExceptionAsync(failAttr.Failure));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionCanceledEventAttributes:
                            var cancelAttr = evt.WorkflowExecutionCanceledEventAttributes;
                            throw new WorkflowFailureException(new CancelledFailureException(
                                new()
                                {
                                    Message = "Workflow cancelled",
                                    CanceledFailureInfo = new() { Details = cancelAttr.Details },
                                },
                                null,
                                Client.Options.DataConverter.PayloadConverter));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionTerminatedEventAttributes:
                            var termAttr = evt.WorkflowExecutionTerminatedEventAttributes;
                            var reason = termAttr.Reason == string.Empty ?
                                "Workflow terminated" : termAttr.Reason;
                            throw new WorkflowFailureException(new TerminatedFailureException(
                                new()
                                {
                                    Message = "Workflow terminated",
                                    TerminatedFailureInfo = new(),
                                },
                                null));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionTimedOutEventAttributes:
                            var timeAttr = evt.WorkflowExecutionTimedOutEventAttributes;
                            if (timeAttr.NewExecutionRunId != string.Empty && followRuns)
                            {
                                histRunID = timeAttr.NewExecutionRunId;
                                break;
                            }
                            throw new WorkflowFailureException(new TimeoutFailureException(
                                new()
                                {
                                    Message = "Workflow timed out",
                                    TimeoutFailureInfo = new()
                                    {
                                        TimeoutType = TimeoutType.StartToClose,
                                    },
                                },
                                null,
                                Client.Options.DataConverter.PayloadConverter));
                    }
                }
                // If we didn't get a new ID to follow, we didn't get a completion event
                if (histRunID == null)
                {
                    throw new InvalidOperationException("No completion event found");
                }
            }
        }

        /// <summary>
        /// Signal a workflow with the given WorkflowSignal attributed method.
        /// </summary>
        /// <param name="signal">Workflow signal method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>
        /// Signal completion task. Means signal was accepted, but may not have been processed by
        /// the workflow yet.
        /// </returns>
        public Task SignalAsync(Func<Task> signal, WorkflowSignalOptions? options = null)
        {
            return SignalAsync(
                Workflow.WorkflowSignalAttribute.Definition.FromMethod(signal.Method).Name,
                new object?[0],
                options);
        }

        /// <summary>
        /// Signal a workflow with the given WorkflowSignal attributed method.
        /// </summary>
        /// <typeparam name="T">Signal argument type.</typeparam>
        /// <param name="signal">Workflow signal method.</param>
        /// <param name="arg">Signal argument.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>
        /// Signal completion task. Means signal was accepted, but may not have been processed by
        /// the workflow yet.
        /// </returns>
        public Task SignalAsync<T>(
            Func<T, Task> signal, T arg, WorkflowSignalOptions? options = null)
        {
            return SignalAsync(
                Workflow.WorkflowSignalAttribute.Definition.FromMethod(signal.Method).Name,
                new object?[] { arg },
                options);
        }

        /// <summary>
        /// Signal a workflow with the given signal name and args.
        /// </summary>
        /// <param name="signal">Signal name.</param>
        /// <param name="args">Signal args.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>
        /// Signal completion task. Means signal was accepted, but may not have been processed by
        /// the workflow yet.
        /// </returns>
        public Task SignalAsync(
            string signal, IReadOnlyCollection<object?> args, WorkflowSignalOptions? options = null)
        {
            return Client.OutboundInterceptor.SignalWorkflowAsync(new(
                ID: ID,
                RunID: RunID,
                Signal: signal,
                Args: args,
                Options: options,
                Headers: null));
        }

        /// <summary>
        /// Query a workflow with the given WorkflowQuery attributed method.
        /// </summary>
        /// <typeparam name="TQueryResult">Query result type.</typeparam>
        /// <param name="query">Workflow query method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Query result.</returns>
        public Task<TQueryResult> QueryAsync<TQueryResult>(
            Func<TQueryResult> query, WorkflowQueryOptions? options = null)
        {
            return QueryAsync<TQueryResult>(
                Workflow.WorkflowQueryAttribute.Definition.FromMethod(query.Method).Name,
                new object?[0],
                options);
        }

        /// <summary>
        /// Query a workflow with the given WorkflowQuery attributed method.
        /// </summary>
        /// <typeparam name="T">Query argument type.</typeparam>
        /// <typeparam name="TQueryResult">Query result type.</typeparam>
        /// <param name="query">Workflow query method.</param>
        /// <param name="arg">Query argument.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Query result.</returns>
        public Task<TQueryResult> QueryAsync<T, TQueryResult>(
            Func<T, TQueryResult> query, T arg, WorkflowQueryOptions? options = null)
        {
            return QueryAsync<TQueryResult>(
                Workflow.WorkflowQueryAttribute.Definition.FromMethod(query.Method).Name,
                new object?[] { arg },
                options);
        }

        /// <summary>
        /// Query a workflow with the given WorkflowQuery attributed method.
        /// </summary>
        /// <typeparam name="TQueryResult">Query result type.</typeparam>
        /// <param name="query">Workflow query method.</param>
        /// <param name="args">Query arguments.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Query result.</returns>
        public Task<TQueryResult> QueryAsync<TQueryResult>(
            string query, IReadOnlyCollection<object?> args, WorkflowQueryOptions? options = null)
        {
            return Client.OutboundInterceptor.QueryWorkflowAsync<TQueryResult>(new(
                ID: ID,
                RunID: RunID,
                Query: query,
                Args: args,
                Options: options,
                Headers: null));
        }

        /*
        TODO(cretz):

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
        */
    }

    /// <summary>
    /// A workflow handle with a known workflow result type.
    /// </summary>
    /// <typeparam name="TResult">Result type of the workflow.</typeparam>
    /// <param name="Client">Client used for workflow handle calls.</param>
    /// <param name="ID">Workflow ID.</param>
    /// <param name="RunID">
    /// Run ID used for signals and queries if present to ensure a very specific run to call. This
    /// is only set when getting a workflow handle, not when starting a workflow.
    /// </param>
    /// <param name="ResultRunID">
    /// Run ID used for get result calls to ensure getting a result starting from this run. This is
    /// set the same as a run ID when getting a workflow handle. When starting a workflow, this is
    /// set as the resulting run ID.
    /// </param>
    /// <param name="FirstExecutionRunID">
    /// Run ID used for cancellation and termination to ensure they happen on a workflow starting
    /// with this run ID. This can be set when getting a workflow handle. When starting a workflow,
    /// this is set as the resulting run ID if no start signal was provided.
    /// </param>
    public record WorkflowHandle<TResult>(
        ITemporalClient Client,
        string ID,
        string? RunID = null,
        string? ResultRunID = null,
        string? FirstExecutionRunID = null) :
            WorkflowHandle(Client, ID, RunID, ResultRunID, FirstExecutionRunID)
    {
        /// <summary>
        /// Get the result of a workflow, deserializing into the known result type.
        /// </summary>
        /// <param name="followRuns">
        /// Whether to follow runs until the latest workflow is reached.
        /// </param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Untyped task for waiting on result.</returns>
        /// <exception cref="WorkflowFailureException">
        /// Exception thrown for unsuccessful workflow result. The cause can be
        /// <see cref="CancelledFailureException" />, <see cref="TerminatedFailureException" />,
        /// <see cref="TimeoutFailureException" />, or any exception deserialized that was thrown in
        /// the workflow (usually an <see cref="ApplicationFailureException" />).
        /// </exception>
        public new Task<TResult> GetResultAsync(
            bool followRuns = true, RpcOptions? rpcOptions = null)
        {
            return GetResultAsync<TResult>(followRuns, rpcOptions);
        }
    }
}