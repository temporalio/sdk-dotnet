using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Exceptions;

#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Threading;
#endif

namespace Temporalio.Client
{
    /// <summary>
    /// Workflow handle to perform actions on an individual workflow.
    /// </summary>
    /// <param name="Client">Client used for workflow handle calls.</param>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">
    /// Run ID used for signals, queries, and updates if present to ensure a very specific run to
    /// call. This is only set when getting a workflow handle, not when starting a workflow.
    /// </param>
    /// <param name="ResultRunId">
    /// Run ID used for get result calls to ensure getting a result starting from this run. This is
    /// set the same as a run ID when getting a workflow handle. When starting a workflow, this is
    /// set as the resulting run ID.
    /// </param>
    /// <param name="FirstExecutionRunId">
    /// Run ID used for cancellation and termination to ensure they happen on a workflow starting
    /// with this run ID. This can be set when getting a workflow handle. When starting a workflow,
    /// this is set as the resulting run ID if no start signal was provided.
    /// </param>
    public record WorkflowHandle(
        ITemporalClient Client,
        string Id,
        string? RunId = null,
        string? ResultRunId = null,
        string? FirstExecutionRunId = null)
    {
        /// <summary>
        /// Get the result of a workflow disregarding its return (or not having a return type).
        /// </summary>
        /// <param name="followRuns">
        /// Whether to follow runs until the latest workflow is reached.
        /// </param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Untyped task for waiting on result.</returns>
        /// <exception cref="WorkflowFailedException">
        /// Exception thrown for unsuccessful workflow result. The cause can be
        /// <see cref="CanceledFailureException" />, <see cref="TerminatedFailureException" />,
        /// <see cref="TimeoutFailureException" />, or any exception deserialized that was thrown in
        /// the workflow (usually an <see cref="ApplicationFailureException" />).
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task GetResultAsync(
            bool followRuns = true, RpcOptions? rpcOptions = null) =>
            GetResultAsync<ValueTuple>(followRuns, rpcOptions);

        /// <summary>
        /// Get the result of a workflow, deserializing into the given return type.
        /// </summary>
        /// <typeparam name="TResult">Return type to deserialize result into.</typeparam>
        /// <param name="followRuns">
        /// Whether to follow runs until the latest workflow is reached.
        /// </param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Result of the workflow.</returns>
        /// <exception cref="WorkflowFailedException">
        /// Exception thrown for unsuccessful workflow result. The cause can be
        /// <see cref="CanceledFailureException" />, <see cref="TerminatedFailureException" />,
        /// <see cref="TimeoutFailureException" />, or any exception deserialized that was thrown in
        /// the workflow (usually an <see cref="ApplicationFailureException" />).
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        public virtual async Task<TResult> GetResultAsync<TResult>(
            bool followRuns = true, RpcOptions? rpcOptions = null)
        {
            // Continually get pages
            var histRunId = ResultRunId;
            while (true)
            {
                var page = await Client.OutboundInterceptor.FetchWorkflowHistoryEventPageAsync(new(
                    Id: Id,
                    RunId: histRunId,
                    PageSize: 0,
                    NextPageToken: null,
                    WaitNewEvent: true,
                    EventFilterType: HistoryEventFilterType.CloseEvent,
                    SkipArchival: true,
                    Rpc: TemporalClient.DefaultRetryOptions(rpcOptions))).ConfigureAwait(false);
                if (page.Events.Count == 0)
                {
                    throw new InvalidOperationException("Event set unexpectedly empty");
                }
                histRunId = null;
                foreach (var evt in page.Events)
                {
                    switch (evt.AttributesCase)
                    {
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionCompletedEventAttributes:
                            var compAttr = evt.WorkflowExecutionCompletedEventAttributes;
                            if (!string.IsNullOrEmpty(compAttr.NewExecutionRunId) && followRuns)
                            {
                                histRunId = compAttr.NewExecutionRunId;
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
                                compAttr.Result.Payloads_).ConfigureAwait(false);
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionFailedEventAttributes:
                            var failAttr = evt.WorkflowExecutionFailedEventAttributes;
                            if (!string.IsNullOrEmpty(failAttr.NewExecutionRunId) && followRuns)
                            {
                                histRunId = failAttr.NewExecutionRunId;
                                break;
                            }
                            throw new WorkflowFailedException(
                                await Client.Options.DataConverter.ToExceptionAsync(
                                    failAttr.Failure).ConfigureAwait(false));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionCanceledEventAttributes:
                            var cancelAttr = evt.WorkflowExecutionCanceledEventAttributes;
                            throw new WorkflowFailedException(new CanceledFailureException(
                                new()
                                {
                                    Message = "Workflow canceled",
                                    CanceledFailureInfo = new() { Details = cancelAttr.Details },
                                },
                                null,
                                Client.Options.DataConverter.PayloadConverter));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionTerminatedEventAttributes:
                            var termAttr = evt.WorkflowExecutionTerminatedEventAttributes;
                            var message = string.IsNullOrEmpty(termAttr.Reason) ?
                                "Workflow terminated" : termAttr.Reason;
                            InboundFailureDetails? details = null;
                            if (termAttr.Details != null && termAttr.Details.Payloads_.Count > 0)
                            {
                                details = new(
                                    Client.Options.DataConverter.PayloadConverter,
                                    termAttr.Details.Payloads_);
                            }
                            throw new WorkflowFailedException(new TerminatedFailureException(
                                new() { Message = message, TerminatedFailureInfo = new() },
                                null,
                                details));
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionTimedOutEventAttributes:
                            var timeAttr = evt.WorkflowExecutionTimedOutEventAttributes;
                            if (!string.IsNullOrEmpty(timeAttr.NewExecutionRunId) && followRuns)
                            {
                                histRunId = timeAttr.NewExecutionRunId;
                                break;
                            }
                            throw new WorkflowFailedException(new TimeoutFailureException(
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
                        case HistoryEvent.AttributesOneofCase.WorkflowExecutionContinuedAsNewEventAttributes:
                            var contAttr = evt.WorkflowExecutionContinuedAsNewEventAttributes;
                            if (string.IsNullOrEmpty(contAttr.NewExecutionRunId))
                            {
                                throw new InvalidOperationException("Continue as new missing new run ID");
                            }
                            else if (followRuns)
                            {
                                histRunId = contAttr.NewExecutionRunId;
                                break;
                            }
                            throw new WorkflowContinuedAsNewException(contAttr.NewExecutionRunId);
                    }
                }
                // If we didn't get a new ID to follow, we didn't get a completion event
                if (histRunId == null)
                {
                    throw new InvalidOperationException("No completion event found");
                }
            }
        }

        /// <summary>
        /// Signal a workflow via a lambda call to a WorkflowSignal attributed method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="signalCall">Invocation of a workflow signal method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>
        /// Signal completion task. Means signal was accepted, but may not have been processed by
        /// the workflow yet.
        /// </returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task SignalAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> signalCall, WorkflowSignalOptions? options = null)
        {
            var (method, args) = ExpressionUtil.ExtractCall(signalCall);
            return SignalAsync(
                Workflows.WorkflowSignalDefinition.NameFromMethodForCall(method),
                args,
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
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task SignalAsync(
            string signal, IReadOnlyCollection<object?> args, WorkflowSignalOptions? options = null) =>
            Client.OutboundInterceptor.SignalWorkflowAsync(new(
                Id: Id,
                RunId: RunId,
                Signal: signal,
                Args: args,
                Options: options,
                Headers: null));

        /// <summary>
        /// Query a workflow via a lambda that calls a WorkflowQuery attributed method or accesses
        /// a WorkflowQuery attributed property.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TQueryResult">Query result type.</typeparam>
        /// <param name="queryCall">Invocation of a workflow query method or access of workflow
        /// query property.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Query result.</returns>
        /// <exception cref="WorkflowQueryFailedException">Query failed on worker.</exception>
        /// <exception cref="WorkflowQueryRejectedException">
        /// Query rejected by server based on rejection condition.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task<TQueryResult> QueryAsync<TWorkflow, TQueryResult>(
            Expression<Func<TWorkflow, TQueryResult>> queryCall,
            WorkflowQueryOptions? options = null)
        {
            // Try property first
            var member = ExpressionUtil.ExtractMemberAccess(queryCall);
            if (member != null)
            {
                if (member is not PropertyInfo property)
                {
                    throw new ArgumentException("Expression must be a single method call or property access");
                }
                return QueryAsync<TQueryResult>(
                    Workflows.WorkflowQueryDefinition.NameFromPropertyForCall(property),
                    Array.Empty<object?>(),
                    options);
            }
            // Try method
            var (method, args) = ExpressionUtil.ExtractCall(
                queryCall, errorSaysPropertyAccepted: true);
            return QueryAsync<TQueryResult>(
                Workflows.WorkflowQueryDefinition.NameFromMethodForCall(method),
                args,
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
        /// <exception cref="WorkflowQueryFailedException">Query failed on worker.</exception>
        /// <exception cref="WorkflowQueryRejectedException">
        /// Query rejected by server based on rejection condition.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task<TQueryResult> QueryAsync<TQueryResult>(
            string query, IReadOnlyCollection<object?> args, WorkflowQueryOptions? options = null) =>
            Client.OutboundInterceptor.QueryWorkflowAsync<TQueryResult>(new(
                Id: Id,
                RunId: RunId,
                Query: query,
                Args: args,
                Options: options,
                Headers: null));

        /// <summary>
        /// Start a workflow update via a call to a WorkflowUpdate attributed method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public Task<WorkflowUpdateHandle> StartUpdateAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> updateCall, WorkflowUpdateStartOptions options)
        {
            var (method, args) = ExpressionUtil.ExtractCall(updateCall);
            return StartUpdateAsync(
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <summary>
        /// Start a workflow update via a call to a WorkflowUpdate attributed method.
        /// </summary>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateAsync<TWorkflow, TUpdateResult>(
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowUpdateStartOptions options)
        {
            var (method, args) = ExpressionUtil.ExtractCall(updateCall);
            return StartUpdateAsync<TUpdateResult>(
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <summary>
        /// Start a workflow update using its name.
        /// </summary>
        /// <param name="update">Name of the update.</param>
        /// <param name="args">Arguments for the update.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public async Task<WorkflowUpdateHandle> StartUpdateAsync(
            string update, IReadOnlyCollection<object?> args, WorkflowUpdateStartOptions options) =>
            await StartUpdateAsync<ValueTuple>(update, args, options).ConfigureAwait(false);

        /// <summary>
        /// Start a workflow update using its name.
        /// </summary>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="update">Name of the update.</param>
        /// <param name="args">Arguments for the update.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateAsync<TUpdateResult>(
            string update, IReadOnlyCollection<object?> args, WorkflowUpdateStartOptions options) =>
            Client.OutboundInterceptor.StartWorkflowUpdateAsync<TUpdateResult>(new(
                Id: Id,
                RunId: RunId,
                FirstExecutionRunId: FirstExecutionRunId,
                Update: update,
                Args: args,
                Options: options,
                Headers: null));

        /// <summary>
        /// Start an update and wait for it to complete. This is a shortcut for
        /// <see cref="StartUpdateAsync{TWorkflow}(Expression{Func{TWorkflow, Task}}, WorkflowUpdateStartOptions)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)" />.
        /// </summary>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change. Currently this
        /// API will timeout on long update requests instead of properly polling for their
        /// completion.</remarks>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public async Task ExecuteUpdateAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            // TODO(cretz): Support timeout for the life of the call when polling works
            var handle = await StartUpdateAsync(
                updateCall, UpdateOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update and wait for it to complete. This is a shortcut for
        /// <see cref="StartUpdateAsync{TWorkflow, TUpdateResult}(Expression{Func{TWorkflow, Task{TUpdateResult}}}, WorkflowUpdateStartOptions)" />
        /// +
        /// <see cref="WorkflowUpdateHandle{TResult}.GetResultAsync(RpcOptions?)" />.
        /// </summary>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change. Currently this
        /// API will timeout on long update requests instead of properly polling for their
        /// completion.</remarks>
        /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public async Task<TUpdateResult> ExecuteUpdateAsync<TWorkflow, TUpdateResult>(
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await StartUpdateAsync(
                updateCall, UpdateOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update and wait for it to complete. This is a shortcut for
        /// <see cref="StartUpdateAsync(string, IReadOnlyCollection{object?}, WorkflowUpdateStartOptions)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)" />.
        /// </summary>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change. Currently this
        /// API will timeout on long update requests instead of properly polling for their
        /// completion.</remarks>
        /// <param name="update">Update name.</param>
        /// <param name="args">Update args.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public async Task ExecuteUpdateAsync(
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await StartUpdateAsync(
                update, args, UpdateOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update and wait for it to complete. This is a shortcut for
        /// <see cref="StartUpdateAsync{TUpdateResult}(string, IReadOnlyCollection{object?}, WorkflowUpdateStartOptions)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)" />.
        /// </summary>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change. Currently this
        /// API will timeout on long update requests instead of properly polling for their
        /// completion.</remarks>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="update">Update name.</param>
        /// <param name="args">Update args.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public async Task<TUpdateResult> ExecuteUpdateAsync<TUpdateResult>(
            string update,
            IReadOnlyCollection<object?> args,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await StartUpdateAsync<TUpdateResult>(
                update, args, UpdateOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a workflow update handle for the given update ID on this workflow.
        /// </summary>
        /// <param name="id">ID of the update.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public WorkflowUpdateHandle GetUpdateHandle(string id) =>
            new(Client: Client, Id: id, WorkflowId: Id, WorkflowRunId: RunId);

        /// <summary>
        /// Get a workflow update handle for the given update ID on this workflow.
        /// </summary>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="id">ID of the update.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public WorkflowUpdateHandle<TUpdateResult> GetUpdateHandle<TUpdateResult>(string id) =>
            new(Client: Client, Id: id, WorkflowId: Id, WorkflowRunId: RunId);

        /// <summary>
        /// Get the current description of this workflow.
        /// </summary>
        /// <param name="options">Extra options.</param>
        /// <returns>Description for the workflow.</returns>
        public Task<WorkflowExecutionDescription> DescribeAsync(
            WorkflowDescribeOptions? options = null) =>
            Client.OutboundInterceptor.DescribeWorkflowAsync(new(
                Id: Id,
                RunId: RunId,
                Options: options));

        /// <summary>
        /// Request cancellation of this workflow.
        /// </summary>
        /// <param name="options">Cancellation options.</param>
        /// <returns>Cancel accepted task.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task CancelAsync(WorkflowCancelOptions? options = null) =>
            Client.OutboundInterceptor.CancelWorkflowAsync(new(
                Id: Id,
                RunId: RunId,
                FirstExecutionRunId: FirstExecutionRunId,
                Options: options));

        /// <summary>
        /// Terminate this workflow.
        /// </summary>
        /// <param name="reason">Termination reason.</param>
        /// <param name="options">Termination options.</param>
        /// <returns>Terminate completed task.</returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task TerminateAsync(
            string? reason = null, WorkflowTerminateOptions? options = null) =>
            Client.OutboundInterceptor.TerminateWorkflowAsync(new(
                Id: Id,
                RunId: RunId,
                FirstExecutionRunId: FirstExecutionRunId,
                Reason: reason,
                Options: options));

#if NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// Fetcgh history for the workflow.
        /// </summary>
        /// <param name="options">Options for history fetching.</param>
        /// <returns>Fetched history.</returns>
        public async Task<WorkflowHistory> FetchHistoryAsync(
            WorkflowHistoryEventFetchOptions? options = null)
        {
            WorkflowHistoryEventFetchOptions? eventFetchOptions = null;
            if (options != null)
            {
                eventFetchOptions = new()
                {
                    EventFilterType = options.EventFilterType,
                    SkipArchival = options.SkipArchival,
                    Rpc = options.Rpc,
                };
            }
            var events = new List<HistoryEvent>();
            await foreach (var evt in FetchHistoryEventsAsync(eventFetchOptions).ConfigureAwait(false))
            {
                events.Add(evt);
            }
            return new(Id, events);
        }

        /// <summary>
        /// Asynchronously iterate over history events.
        /// </summary>
        /// <param name="options">History event fetch options.</param>
        /// <returns>Async enumerable to iterate events for.</returns>
        public IAsyncEnumerable<HistoryEvent> FetchHistoryEventsAsync(
            WorkflowHistoryEventFetchOptions? options = null) =>
            FetchHistoryEventsInternalAsync(options);

        private async IAsyncEnumerable<HistoryEvent> FetchHistoryEventsInternalAsync(
            WorkflowHistoryEventFetchOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Need to combine cancellation token
            var rpcOptsAndCancelSource = (options?.Rpc ?? TemporalClient.RetryRpcOptions).
                WithAdditionalCancellationToken(cancellationToken);
            try
            {
                byte[]? nextPageToken = null;
                do
                {
                    var page = await Client.OutboundInterceptor.FetchWorkflowHistoryEventPageAsync(new(
                        Id: Id,
                        RunId: RunId,
                        PageSize: 0,
                        NextPageToken: nextPageToken,
                        WaitNewEvent: options?.WaitNewEvent ?? false,
                        EventFilterType: options?.EventFilterType ?? HistoryEventFilterType.AllEvent,
                        SkipArchival: options?.SkipArchival ?? false,
                        Rpc: rpcOptsAndCancelSource.Item1)).ConfigureAwait(false);
                    foreach (var evt in page.Events)
                    {
                        yield return evt;
                    }
                    nextPageToken = page.NextPageToken;
                }
                while (nextPageToken != null);
            }
            finally
            {
                rpcOptsAndCancelSource.Item2?.Dispose();
            }
        }
#endif

        /// <summary>
        /// Create options with defaults automatically set for ExecuteUpdateAsync.
        /// </summary>
        /// <param name="options">Options to use as base.</param>
        /// <returns>New options.</returns>
        private protected static WorkflowUpdateStartOptions UpdateOptionsWithDefaultsForExecute(
            WorkflowUpdateOptions? options) =>
            (WorkflowUpdateStartOptions)new WorkflowUpdateStartOptions()
            {
                Id = options?.Id,
                Rpc = options?.Rpc,
                WaitForStage = WorkflowUpdateStage.Completed,
            }.Clone();
    }

    /// <summary>
    /// A workflow handle with a known workflow type.
    /// </summary>
    /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
    /// <param name="Client">Client used for workflow handle calls.</param>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">
    /// Run ID used for signals and queries if present to ensure a very specific run to call. This
    /// is only set when getting a workflow handle, not when starting a workflow.
    /// </param>
    /// <param name="ResultRunId">
    /// Run ID used for get result calls to ensure getting a result starting from this run. This is
    /// set the same as a run ID when getting a workflow handle. When starting a workflow, this is
    /// set as the resulting run ID.
    /// </param>
    /// <param name="FirstExecutionRunId">
    /// Run ID used for cancellation and termination to ensure they happen on a workflow starting
    /// with this run ID. This can be set when getting a workflow handle. When starting a workflow,
    /// this is set as the resulting run ID if no start signal was provided.
    /// </param>
    public record WorkflowHandle<TWorkflow>(
        ITemporalClient Client,
        string Id,
        string? RunId = null,
        string? ResultRunId = null,
        string? FirstExecutionRunId = null) :
            WorkflowHandle(Client, Id, RunId, ResultRunId, FirstExecutionRunId)
    {
        /// <summary>
        /// Signal a workflow via a lambda call to a WorkflowSignal attributed method.
        /// </summary>
        /// <param name="signalCall">Invocation of a workflow signal method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>
        /// Signal completion task. Means signal was accepted, but may not have been processed by
        /// the workflow yet.
        /// </returns>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task SignalAsync(
            Expression<Func<TWorkflow, Task>> signalCall, WorkflowSignalOptions? options = null) =>
            SignalAsync<TWorkflow>(signalCall, options);

        /// <summary>
        /// Query a workflow via a lambda that calls a WorkflowQuery attributed method or accesses
        /// a WorkflowQuery attributed property.
        /// </summary>
        /// <typeparam name="TQueryResult">Query result type.</typeparam>
        /// <param name="queryCall">Invocation of a workflow query method or access of workflow
        /// query property.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Query result.</returns>
        /// <exception cref="WorkflowQueryFailedException">Query failed on worker.</exception>
        /// <exception cref="WorkflowQueryRejectedException">
        /// Query rejected by server based on rejection condition.
        /// </exception>
        /// <exception cref="RpcException">Server-side error.</exception>
        public Task<TQueryResult> QueryAsync<TQueryResult>(
            Expression<Func<TWorkflow, TQueryResult>> queryCall,
            WorkflowQueryOptions? options = null) =>
            QueryAsync<TWorkflow, TQueryResult>(queryCall, options);

        /// <summary>
        /// Start a workflow update via a call to a WorkflowUpdate attributed method.
        /// </summary>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public Task<WorkflowUpdateHandle> StartUpdateAsync(
            Expression<Func<TWorkflow, Task>> updateCall, WorkflowUpdateStartOptions options) =>
            StartUpdateAsync<TWorkflow>(updateCall, options);

        /// <summary>
        /// Start a workflow update via a call to a WorkflowUpdate attributed method.
        /// </summary>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Update options. Currently <c>WaitForStage</c> is required.</param>
        /// <returns>Workflow update handle.</returns>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change.</remarks>
        public Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateAsync<TUpdateResult>(
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowUpdateStartOptions options) =>
            StartUpdateAsync<TWorkflow, TUpdateResult>(updateCall, options);

        /// <summary>
        /// Start an update and wait for it to complete. This is a shortcut for
        /// <see cref="StartUpdateAsync(Expression{Func{TWorkflow, Task}}, WorkflowUpdateStartOptions)" />
        /// +
        /// <see cref="WorkflowUpdateHandle.GetResultAsync(RpcOptions?)" />.
        /// </summary>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change. Currently this
        /// API will timeout on long update requests instead of properly polling for their
        /// completion.</remarks>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public async Task ExecuteUpdateAsync(
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await StartUpdateAsync(
                updateCall, UpdateOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }

        /// <summary>
        /// Start an update and wait for it to complete. This is a shortcut for
        /// <see cref="StartUpdateAsync{TUpdateResult}(Expression{Func{TWorkflow, Task{TUpdateResult}}}, WorkflowUpdateStartOptions)" />
        /// +
        /// <see cref="WorkflowUpdateHandle{TResult}.GetResultAsync(RpcOptions?)" />.
        /// </summary>
        /// <remarks>WARNING: Workflow update is experimental and APIs may change. Currently this
        /// API will timeout on long update requests instead of properly polling for their
        /// completion.</remarks>
        /// <typeparam name="TUpdateResult">Update result type.</typeparam>
        /// <param name="updateCall">Invocation of workflow update method.</param>
        /// <param name="options">Extra options.</param>
        /// <returns>Completed update task.</returns>
        public async Task<TUpdateResult> ExecuteUpdateAsync<TUpdateResult>(
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowUpdateOptions? options = null)
        {
            var handle = await StartUpdateAsync(
                updateCall, UpdateOptionsWithDefaultsForExecute(options)).ConfigureAwait(false);
            return await handle.GetResultAsync(rpcOptions: options?.Rpc).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A workflow handle with a known workflow type and a known workflow result type.
    /// </summary>
    /// <typeparam name="TWorkflow">Workflow class type.</typeparam>
    /// <typeparam name="TResult">Result type of the workflow.</typeparam>
    /// <param name="Client">Client used for workflow handle calls.</param>
    /// <param name="Id">Workflow ID.</param>
    /// <param name="RunId">
    /// Run ID used for signals, queries, and updates if present to ensure a very specific run to
    /// call. This is only set when getting a workflow handle, not when starting a workflow.
    /// </param>
    /// <param name="ResultRunId">
    /// Run ID used for get result calls to ensure getting a result starting from this run. This is
    /// set the same as a run ID when getting a workflow handle. When starting a workflow, this is
    /// set as the resulting run ID.
    /// </param>
    /// <param name="FirstExecutionRunId">
    /// Run ID used for cancellation and termination to ensure they happen on a workflow starting
    /// with this run ID. This can be set when getting a workflow handle. When starting a workflow,
    /// this is set as the resulting run ID if no start signal was provided.
    /// </param>
    public record WorkflowHandle<TWorkflow, TResult>(
        ITemporalClient Client,
        string Id,
        string? RunId = null,
        string? ResultRunId = null,
        string? FirstExecutionRunId = null) :
            WorkflowHandle<TWorkflow>(Client, Id, RunId, ResultRunId, FirstExecutionRunId)
    {
        /// <summary>
        /// Get the result of a workflow, deserializing into the known result type.
        /// </summary>
        /// <param name="followRuns">
        /// Whether to follow runs until the latest workflow is reached.
        /// </param>
        /// <param name="rpcOptions">RPC options.</param>
        /// <returns>Result of the workflow.</returns>
        /// <exception cref="WorkflowFailedException">
        /// Exception thrown for unsuccessful workflow result. The cause can be
        /// <see cref="CanceledFailureException" />, <see cref="TerminatedFailureException" />,
        /// <see cref="TimeoutFailureException" />, or any exception deserialized that was thrown in
        /// the workflow (usually an <see cref="ApplicationFailureException" />).
        /// </exception>
        public new Task<TResult> GetResultAsync(
            bool followRuns = true, RpcOptions? rpcOptions = null) =>
            GetResultAsync<TResult>(followRuns, rpcOptions);
    }
}