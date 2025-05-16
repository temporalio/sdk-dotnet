using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Api.TaskQueue.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client.Interceptors;
using Temporalio.Common;
using Temporalio.Converters;
using Temporalio.Exceptions;

#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Threading;
#endif

namespace Temporalio.Client
{
    public partial class TemporalClient
    {
        /// <inheritdoc />
        public async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return await OutboundInterceptor.StartWorkflowAsync<TWorkflow, TResult>(new(
                Workflow: Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                Args: args,
                Options: options,
                Headers: null)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<WorkflowHandle<TWorkflow>> StartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> workflowRunCall, WorkflowOptions options)
        {
            var (runMethod, args) = Common.ExpressionUtil.ExtractCall(workflowRunCall);
            return await OutboundInterceptor.StartWorkflowAsync<TWorkflow, ValueTuple>(new(
                Workflow: Workflows.WorkflowDefinition.NameFromRunMethodForCall(runMethod),
                Args: args,
                Options: options,
                Headers: null)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<WorkflowHandle> StartWorkflowAsync(
            string workflow, IReadOnlyCollection<object?> args, WorkflowOptions options) =>
            await OutboundInterceptor.StartWorkflowAsync<ValueTuple, ValueTuple>(new(
                Workflow: workflow,
                Args: args,
                Options: options,
                Headers: null)).ConfigureAwait(false);

        /// <inheritdoc />
        public WorkflowHandle GetWorkflowHandle(
            string id, string? runId = null, string? firstExecutionRunId = null) =>
            new(Client: this, Id: id, RunId: runId, FirstExecutionRunId: firstExecutionRunId);

        /// <inheritdoc />
        public WorkflowHandle<TWorkflow> GetWorkflowHandle<TWorkflow>(
            string id, string? runId = null, string? firstExecutionRunId = null) =>
            new(Client: this, Id: id, RunId: runId, FirstExecutionRunId: firstExecutionRunId);

        /// <inheritdoc />
        public WorkflowHandle<TWorkflow, TResult> GetWorkflowHandle<TWorkflow, TResult>(
            string id, string? runId = null, string? firstExecutionRunId = null) =>
            new(Client: this, Id: id, RunId: runId, FirstExecutionRunId: firstExecutionRunId);

        /// <inheritdoc />
        public Task<WorkflowUpdateHandle> StartUpdateWithStartWorkflowAsync<TWorkflow>(
            Expression<Func<TWorkflow, Task>> updateCall,
            WorkflowStartUpdateWithStartOptions options)
        {
            var (method, args) = ExpressionUtil.ExtractCall(updateCall);
            return StartUpdateWithStartWorkflowAsync(
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <inheritdoc />
        public Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateWithStartWorkflowAsync<TWorkflow, TUpdateResult>(
            Expression<Func<TWorkflow, Task<TUpdateResult>>> updateCall,
            WorkflowStartUpdateWithStartOptions options)
        {
            var (method, args) = ExpressionUtil.ExtractCall(updateCall);
            return StartUpdateWithStartWorkflowAsync<TUpdateResult>(
                Workflows.WorkflowUpdateDefinition.NameFromMethodForCall(method),
                args,
                options);
        }

        /// <inheritdoc />
        public async Task<WorkflowUpdateHandle> StartUpdateWithStartWorkflowAsync(
            string update, IReadOnlyCollection<object?> args, WorkflowStartUpdateWithStartOptions options) =>
            await StartUpdateWithStartWorkflowAsync<ValueTuple>(update, args, options).ConfigureAwait(false);

        /// <inheritdoc />
        public Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateWithStartWorkflowAsync<TUpdateResult>(
            string update, IReadOnlyCollection<object?> args, WorkflowStartUpdateWithStartOptions options) =>
            OutboundInterceptor.StartUpdateWithStartWorkflowAsync<TUpdateResult>(new(
                Update: update,
                Args: args,
                Options: options,
                Headers: null));

#if NETCOREAPP3_0_OR_GREATER
        /// <inheritdoc />
        public IAsyncEnumerable<WorkflowExecution> ListWorkflowsAsync(
            string query, WorkflowListOptions? options = null) =>
            OutboundInterceptor.ListWorkflowsAsync(new(Query: query, Options: options));
#endif

        /// <inheritdoc />
        public Task<WorkflowExecutionCount> CountWorkflowsAsync(
            string query, WorkflowCountOptions? options = null) =>
            OutboundInterceptor.CountWorkflowsAsync(new(Query: query, Options: options));

        internal partial class Impl
        {
            private static IReadOnlyCollection<HistoryEvent> emptyEvents = new List<HistoryEvent>(0);

            /// <inheritdoc />
            public override async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
                StartWorkflowInput input)
            {
                try
                {
                    return await StartWorkflowInternalAsync<TWorkflow, TResult>(
                        input).ConfigureAwait(false);
                }
                catch (RpcException e) when (
                    e.Code == RpcException.StatusCode.AlreadyExists)
                {
                    // Throw already started if there is a single detail with the expected type
                    var status = e.GrpcStatus.Value;
                    if (status != null && status.Details.Count == 1)
                    {
                        if (status.Details[0].TryUnpack(out Api.ErrorDetails.V1.WorkflowExecutionAlreadyStartedFailure failure))
                        {
                            throw new WorkflowAlreadyStartedException(
                                e.Message,
                                workflowId: input.Options.Id!,
                                workflowType: input.Workflow,
                                runId: failure.RunId);
                        }
                    }
                    throw;
                }
            }

            /// <inheritdoc />
            public override async Task<WorkflowUpdateHandle<TUpdateResult>> StartUpdateWithStartWorkflowAsync<TUpdateResult>(
                StartUpdateWithStartWorkflowInput input)
            {
                // Try to mark used before using
                if (input.Options.StartWorkflowOperation == null)
                {
                    throw new ArgumentException("Start workflow operation is required in options");
                }
                if (!input.Options.StartWorkflowOperation.TryMarkUsed())
                {
                    throw new ArgumentException("Start operation already used");
                }
                // Workflow-specific data converter
                var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                    new ISerializationContext.Workflow(
                        Namespace: Client.Options.Namespace,
                        WorkflowId: input.Options.StartWorkflowOperation.Options.Id ?? throw new ArgumentException("ID required to start workflow")));

                // We choose to put everything in one large try statement because we want to make
                // sure that any failures are also propagated to the waiter of the handle too, not
                // just thrown out of here.
                try
                {
                    // Disallow some options in start that don't work here, and require others
                    if (input.Options.StartWorkflowOperation.Options.StartSignal != null ||
                        input.Options.StartWorkflowOperation.Options.StartSignalArgs != null)
                    {
                        throw new ArgumentException("Cannot have start signal on update with start");
                    }
                    if (input.Options.StartWorkflowOperation.Options.RequestEagerStart)
                    {
                        throw new ArgumentException("Cannot request eager start on update with start");
                    }
                    if (input.Options.StartWorkflowOperation.Options.IdConflictPolicy == WorkflowIdConflictPolicy.Unspecified)
                    {
                        throw new ArgumentException("Workflow ID conflict policy required for update with start");
                    }
                    if (input.Options.StartWorkflowOperation.Options.Rpc != null)
                    {
                        throw new ArgumentException("Cannot set RPC options on start options, set them on the update options");
                    }

                    // Build request
                    var startReq = await CreateStartWorkflowRequestAsync(
                        input.Options.StartWorkflowOperation.Workflow,
                        input.Options.StartWorkflowOperation.Args,
                        input.Options.StartWorkflowOperation.Options,
                        dataConverter,
                        input.Options.StartWorkflowOperation.Headers).ConfigureAwait(false);
                    var updateReq = await CreateUpdateWorkflowRequestAsync(
                        input.Update,
                        input.Args,
                        input.Options,
                        input.Options.WaitForStage,
                        dataConverter,
                        input.Headers).ConfigureAwait(false);
                    updateReq.WorkflowExecution = new() { WorkflowId = startReq.WorkflowId };
                    var req = new ExecuteMultiOperationRequest() { Namespace = Client.Options.Namespace };
                    req.Operations.Add(
                        new ExecuteMultiOperationRequest.Types.Operation() { StartWorkflow = startReq });
                    req.Operations.Add(
                        new ExecuteMultiOperationRequest.Types.Operation() { UpdateWorkflow = updateReq });

                    // Continually try to start until an exception occurs, the user-asked stage is
                    // reached, or the stage is accepted. But we will set the workflow handle as soon as
                    // we can.
                    UpdateWorkflowExecutionResponse? updateResp = null;
                    string? runId = null;
                    do
                    {
                        var resp = await Client.Connection.WorkflowService.ExecuteMultiOperationAsync(
                            req, DefaultRetryOptions(input.Options.Rpc)).ConfigureAwait(false);
                        // Set start result if not already set
                        runId = resp.Responses[0].StartWorkflow.RunId;
                        if (!input.Options.StartWorkflowOperation.IsCompleted)
                        {
                            input.Options.StartWorkflowOperation.SetResult((WorkflowHandle)Activator.CreateInstance(
                                input.Options.StartWorkflowOperation.HandleType,
                                // Parameters on workflow handles are always: client, ID, run ID,
                                // result run ID, and first execution run ID. This code is tested to
                                // confirm.
                                Client,
                                input.Options.StartWorkflowOperation.Options.Id!,
                                null,
                                runId,
                                runId)!);
                        }
                        updateResp = resp.Responses[1].UpdateWorkflow;
                    }
                    while (updateResp == null || updateResp.Stage < UpdateWorkflowExecutionLifecycleStage.Accepted);

                    // If the requested stage is completed, wait for result, but discard the update
                    // exception, that will come when _they_ call get result
                    var handle = new WorkflowUpdateHandle<TUpdateResult>(
                        Client, updateReq.Request.Meta.UpdateId, input.Options.StartWorkflowOperation.Options.Id!, runId)
                    { KnownOutcome = updateResp.Outcome };
                    if (input.Options.WaitForStage == WorkflowUpdateStage.Completed)
                    {
                        await handle.PollUntilOutcomeAsync(input.Options.Rpc).ConfigureAwait(false);
                    }
                    return handle;
                }
                catch (Exception e)
                {
                    // If this is a multi-operation failure, set exception to the first present,
                    // non-OK, non-aborted error
                    if (e is RpcException rpcErr)
                    {
                        var status = rpcErr.GrpcStatus.Value;
                        if (status != null && status.Details.Count == 1)
                        {
                            if (status.Details[0].TryUnpack(out Api.ErrorDetails.V1.MultiOperationExecutionFailure failure))
                            {
                                var nonAborted = failure.Statuses.FirstOrDefault(s =>
                                    // Exists
                                    s != null &&
                                    // Not ok
                                    s.Code != (int)RpcException.StatusCode.OK &&
                                    // Not aborted
                                    (s.Details.Count == 0 ||
                                        !s.Details[0].Is(Api.Failure.V1.MultiOperationExecutionAborted.Descriptor)));
                                if (nonAborted != null)
                                {
                                    var grpcStatus = new GrpcStatus() { Code = nonAborted.Code, Message = nonAborted.Message };
                                    grpcStatus.Details.AddRange(nonAborted.Details);
                                    e = new RpcException(grpcStatus);
                                }
                            }
                        }
                    }

                    // If this is a cancellation, use the update cancel exception
                    if (e is OperationCanceledException || (e is RpcException rpcErr2 && (
                        rpcErr2.Code == RpcException.StatusCode.DeadlineExceeded ||
                        rpcErr2.Code == RpcException.StatusCode.Cancelled)))
                    {
                        e = new WorkflowUpdateRpcTimeoutOrCanceledException(e);
                    }

                    // Create workflow-already-started failure if it is that
                    if (e is RpcException rpcErr3 && rpcErr3.Code == RpcException.StatusCode.AlreadyExists)
                    {
                        var status = rpcErr3.GrpcStatus.Value;
                        if (status != null &&
                            status.Details.Count == 1 &&
                            status.Details[0].TryUnpack(out Api.ErrorDetails.V1.WorkflowExecutionAlreadyStartedFailure failure))
                        {
                            e = new WorkflowAlreadyStartedException(
                                e.Message,
                                // "<unknown>" should never happen if it got an RPC exception
                                workflowId: input.Options.StartWorkflowOperation?.Options?.Id ?? "<unknown>",
                                workflowType: input.Options.StartWorkflowOperation?.Workflow ?? "<unknown>",
                                runId: failure.RunId);
                        }
                    }

                    // Before we throw here, we want to try to set the start operation exception
                    // if it has not already been completed
                    if (input.Options.StartWorkflowOperation?.IsCompleted == false)
                    {
                        input.Options.StartWorkflowOperation.SetException(e);
                    }
                    throw e;
                }
            }

            /// <inheritdoc />
            public override async Task SignalWorkflowAsync(SignalWorkflowInput input)
            {
                // Workflow-specific data converter
                var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                    new ISerializationContext.Workflow(
                        Namespace: Client.Options.Namespace,
                        WorkflowId: input.Id));
                var req = new SignalWorkflowExecutionRequest()
                {
                    Namespace = Client.Options.Namespace,
                    WorkflowExecution = new()
                    {
                        WorkflowId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                    },
                    SignalName = input.Signal,
                    Identity = Client.Connection.Options.Identity,
                    RequestId = Guid.NewGuid().ToString(),
                };
                if (input.Args.Count > 0)
                {
                    req.Input = new Payloads();
                    req.Input.Payloads_.AddRange(
                        await dataConverter.ToPayloadsAsync(input.Args).ConfigureAwait(false));
                }
                if (input.Headers != null)
                {
                    req.Header = new();
                    req.Header.Fields.Add(input.Headers);
                    // If there is a payload codec, use it to encode the headers
                    if (dataConverter.PayloadCodec is IPayloadCodec codec)
                    {
                        foreach (var kvp in req.Header.Fields)
                        {
                            req.Header.Fields[kvp.Key] =
                                await codec.EncodeSingleAsync(kvp.Value).ConfigureAwait(false);
                        }
                    }
                }
                await Client.Connection.WorkflowService.SignalWorkflowExecutionAsync(
                    req, DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
            {
                // Workflow-specific data converter
                var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                    new ISerializationContext.Workflow(
                        Namespace: Client.Options.Namespace,
                        WorkflowId: input.Id));
                var req = new QueryWorkflowRequest()
                {
                    Namespace = Client.Options.Namespace,
                    Execution = new()
                    {
                        WorkflowId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                    },
                    Query = new() { QueryType = input.Query },
                };
                if (input.Args.Count > 0)
                {
                    req.Query.QueryArgs = new Payloads();
                    req.Query.QueryArgs.Payloads_.AddRange(
                        await dataConverter.ToPayloadsAsync(input.Args).ConfigureAwait(false));
                }
                if (input.Options?.RejectCondition != null)
                {
                    req.QueryRejectCondition = (QueryRejectCondition)input.Options.RejectCondition;
                }
                else if (Client.Options.QueryRejectCondition != null)
                {
                    req.QueryRejectCondition = (QueryRejectCondition)Client.Options.QueryRejectCondition;
                }
                if (input.Headers != null)
                {
                    req.Query.Header = new();
                    req.Query.Header.Fields.Add(input.Headers);
                    // If there is a payload codec, use it to encode the headers
                    if (dataConverter.PayloadCodec is IPayloadCodec codec)
                    {
                        foreach (var kvp in req.Query.Header.Fields)
                        {
                            req.Query.Header.Fields[kvp.Key] =
                                await codec.EncodeSingleAsync(kvp.Value).ConfigureAwait(false);
                        }
                    }
                }

                // Invoke
                QueryWorkflowResponse resp;
                try
                {
                    resp = await Client.Connection.WorkflowService.QueryWorkflowAsync(
                        req, DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                }
                catch (RpcException e) when (
                    e.GrpcStatus.Value != null &&
                    e.GrpcStatus.Value.Details.Count == 1 &&
                    e.GrpcStatus.Value.Details[0].Is(Api.ErrorDetails.V1.QueryFailedFailure.Descriptor))
                {
                    throw new WorkflowQueryFailedException(e.Message);
                }

                // Throw rejection if rejected
                if (resp.QueryRejected != null)
                {
                    throw new WorkflowQueryRejectedException(resp.QueryRejected.Status);
                }

                // Use default value if no result present
                if (resp.QueryResult == null || resp.QueryResult.Payloads_.Count == 0)
                {
                    return default!;
                }
                return await dataConverter.ToSingleValueAsync<TResult>(
                    resp.QueryResult.Payloads_).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public async override Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
                StartWorkflowUpdateInput input)
            {
                if (input.Options.WaitForStage == WorkflowUpdateStage.None)
                {
                    throw new ArgumentException("WaitForStage is required to start workflow update");
                }
                else if (input.Options.WaitForStage == WorkflowUpdateStage.Admitted)
                {
                    throw new ArgumentException(
                        "Admitted is not an allowed wait stage to start workflow update");
                }
                // Workflow-specific data converter
                var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                    new ISerializationContext.Workflow(
                        Namespace: Client.Options.Namespace,
                        WorkflowId: input.Id));
                // Build request
                var req = await CreateUpdateWorkflowRequestAsync(
                    input.Update, input.Args, input.Options, input.Options.WaitForStage, dataConverter, input.Headers).ConfigureAwait(false);
                req.WorkflowExecution = new()
                {
                    WorkflowId = input.Id,
                    RunId = input.RunId ?? string.Empty,
                };
                req.FirstExecutionRunId = input.FirstExecutionRunId ?? string.Empty;

                // Continually try to start until the user-asked stage is reached or the stage is
                // accepted
                UpdateWorkflowExecutionResponse resp;
                do
                {
                    try
                    {
                        resp = await Client.Connection.WorkflowService.UpdateWorkflowExecutionAsync(
                            req, DefaultRetryOptions(input.Options.Rpc)).ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is OperationCanceledException ||
                        (e is RpcException rpcErr && (
                            rpcErr.Code == RpcException.StatusCode.DeadlineExceeded ||
                            rpcErr.Code == RpcException.StatusCode.Cancelled)))
                    {
                        throw new WorkflowUpdateRpcTimeoutOrCanceledException(e);
                    }
                }
                while (resp.Stage < UpdateWorkflowExecutionLifecycleStage.Accepted);

                // If the requested stage is completed, wait for result, but discard the update
                // exception, that will come when _they_ call get result
                var handle = new WorkflowUpdateHandle<TResult>(
                    Client, req.Request.Meta.UpdateId, input.Id, resp.UpdateRef.WorkflowExecution.RunId)
                { KnownOutcome = resp.Outcome };
                if (input.Options.WaitForStage == WorkflowUpdateStage.Completed)
                {
                    await handle.PollUntilOutcomeAsync(input.Options.Rpc).ConfigureAwait(false);
                }
                return handle;
            }

            /// <inheritdoc />
            public override async Task<WorkflowExecutionDescription> DescribeWorkflowAsync(
                DescribeWorkflowInput input)
            {
                // Workflow-specific data converter
                var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                    new ISerializationContext.Workflow(
                        Namespace: Client.Options.Namespace,
                        WorkflowId: input.Id));
                var resp = await Client.Connection.WorkflowService.DescribeWorkflowExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        Execution = new()
                        {
                            WorkflowId = input.Id,
                            RunId = input.RunId ?? string.Empty,
                        },
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                return await WorkflowExecutionDescription.FromProtoAsync(
                    resp, dataConverter).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task CancelWorkflowAsync(CancelWorkflowInput input)
            {
                await Client.Connection.WorkflowService.RequestCancelWorkflowExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        WorkflowExecution = new()
                        {
                            WorkflowId = input.Id,
                            RunId = input.RunId ?? string.Empty,
                        },
                        FirstExecutionRunId = input.FirstExecutionRunId ?? string.Empty,
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task TerminateWorkflowAsync(TerminateWorkflowInput input)
            {
                var req = new TerminateWorkflowExecutionRequest()
                {
                    Namespace = Client.Options.Namespace,
                    WorkflowExecution = new()
                    {
                        WorkflowId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                    },
                    Reason = input.Reason ?? string.Empty,
                    FirstExecutionRunId = input.FirstExecutionRunId ?? string.Empty,
                    Identity = Client.Connection.Options.Identity,
                };
                if (input.Options?.Details != null && input.Options?.Details.Count > 0)
                {
                    // Workflow-specific data converter
                    var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                        new ISerializationContext.Workflow(
                            Namespace: Client.Options.Namespace,
                            WorkflowId: input.Id));
                    req.Details = new Payloads();
                    req.Details.Payloads_.AddRange(
                        await dataConverter.ToPayloadsAsync(input.Options.Details).ConfigureAwait(false));
                }
                await Client.Connection.WorkflowService.TerminateWorkflowExecutionAsync(
                    req, DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task<WorkflowHistoryEventPage> FetchWorkflowHistoryEventPageAsync(
                FetchWorkflowHistoryEventPageInput input)
            {
                var req = new GetWorkflowExecutionHistoryRequest()
                {
                    Namespace = Client.Options.Namespace,
                    Execution = new()
                    {
                        WorkflowId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                    },
                    MaximumPageSize = input.PageSize ?? 0,
                    NextPageToken = input.NextPageToken == null ?
                        ByteString.Empty : ByteString.CopyFrom(input.NextPageToken),
                    WaitNewEvent = input.WaitNewEvent,
                    HistoryEventFilterType = input.EventFilterType,
                    SkipArchival = input.SkipArchival,
                };
                // While there is a next token and no events, keep trying
                while (true)
                {
                    var resp = await Client.Connection.WorkflowService.GetWorkflowExecutionHistoryAsync(
                        req, DefaultRetryOptions(input.Rpc)).ConfigureAwait(false);
                    // We don't support raw history
                    if (resp.RawHistory.Count > 0)
                    {
                        throw new InvalidOperationException("Unexpected raw history returned");
                    }
                    // If there is history or no next page token, we're done
                    var pageComplete =
                        // Has events
                        (resp.History != null && resp.History.Events.Count > 0) ||
                        // Has no next page token
                        resp.NextPageToken.IsEmpty;
                    // In Java test server when waiting for new event and we pass the timeout, a
                    // null history (as opposed to empty event set) is sent with no next page token
                    // and we don't want to consider that page complete
                    if (pageComplete && resp.History == null && input.WaitNewEvent)
                    {
                        pageComplete = false;
                    }
                    // Complete if we got any events or if there is no next page token
                    if (pageComplete)
                    {
                        return new WorkflowHistoryEventPage(
                            resp.History?.Events ?? emptyEvents,
                            resp.NextPageToken.IsEmpty ? null : resp.NextPageToken.ToByteArray());
                    }
                    req.NextPageToken = resp.NextPageToken;
                }
            }

#if NETCOREAPP3_0_OR_GREATER
            /// <inheritdoc />
            public override IAsyncEnumerable<WorkflowExecution> ListWorkflowsAsync(
                ListWorkflowsInput input) =>
                ListWorkflowsInternalAsync(input);
#endif

            /// <inheritdoc />
            public override async Task<WorkflowExecutionCount> CountWorkflowsAsync(
                CountWorkflowsInput input)
            {
                var resp = await Client.Connection.WorkflowService.CountWorkflowExecutionsAsync(
                    new() { Namespace = Client.Options.Namespace, Query = input.Query },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                return new(resp);
            }

#if NETCOREAPP3_0_OR_GREATER
            private async IAsyncEnumerable<WorkflowExecution> ListWorkflowsInternalAsync(
                ListWorkflowsInput input,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                // Need to combine cancellation token
                var rpcOptsAndCancelSource = DefaultRetryOptions(input.Options?.Rpc).
                    WithAdditionalCancellationToken(cancellationToken);
                var yielded = 0;
                try
                {
                    var req = new ListWorkflowExecutionsRequest()
                    {
                        // TODO(cretz): Allow setting of page size or next page token?
                        Namespace = Client.Options.Namespace,
                        Query = input.Query,
                    };
                    do
                    {
                        var resp = await Client.Connection.WorkflowService.ListWorkflowExecutionsAsync(
                            req, rpcOptsAndCancelSource.Item1).ConfigureAwait(false);
                        foreach (var exec in resp.Executions)
                        {
                            if (input.Options != null && input.Options.Limit > 0 &&
                                yielded++ >= input.Options.Limit)
                            {
                                yield break;
                            }
                            yield return new(exec, Client.Options.DataConverter, Client.Options.Namespace);
                        }
                        req.NextPageToken = resp.NextPageToken;
                    }
                    while (!req.NextPageToken.IsEmpty);
                }
                finally
                {
                    rpcOptsAndCancelSource.Item2?.Dispose();
                }
            }
#endif

            private async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowInternalAsync<TWorkflow, TResult>(
                StartWorkflowInput input)
            {
                // Workflow-specific data converter
                var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                    new ISerializationContext.Workflow(
                        Namespace: Client.Options.Namespace,
                        WorkflowId: input.Options.Id ?? throw new ArgumentException("ID required to start workflow")));
                // We will build the non-signal-with-start request and convert to signal with start
                // later if needed
                var req = await CreateStartWorkflowRequestAsync(
                    input.Workflow, input.Args, input.Options, dataConverter, input.Headers).ConfigureAwait(false);

                // If not signal with start, just run and return
                if (input.Options.StartSignal == null)
                {
                    if (input.Options.StartSignalArgs != null)
                    {
                        throw new ArgumentException("Cannot have start signal args without start signal");
                    }
                    var resp = await Client.Connection.WorkflowService.StartWorkflowExecutionAsync(
                        req, DefaultRetryOptions(input.Options.Rpc)).ConfigureAwait(false);
                    return new WorkflowHandle<TWorkflow, TResult>(
                        Client: Client,
                        Id: req.WorkflowId,
                        ResultRunId: resp.RunId,
                        FirstExecutionRunId: resp.RunId);
                }

                // Since it's signal with start, convert and run
                var signalReq = new SignalWithStartWorkflowExecutionRequest()
                {
                    Namespace = req.Namespace,
                    WorkflowId = req.WorkflowId,
                    WorkflowType = req.WorkflowType,
                    TaskQueue = req.TaskQueue,
                    Input = req.Input,
                    WorkflowExecutionTimeout = req.WorkflowExecutionTimeout,
                    WorkflowRunTimeout = req.WorkflowRunTimeout,
                    WorkflowTaskTimeout = req.WorkflowTaskTimeout,
                    Identity = req.Identity,
                    RequestId = req.RequestId,
                    WorkflowIdReusePolicy = req.WorkflowIdReusePolicy,
                    RetryPolicy = req.RetryPolicy,
                    CronSchedule = req.CronSchedule,
                    Memo = req.Memo,
                    SearchAttributes = req.SearchAttributes,
                    Header = req.Header,
                    WorkflowStartDelay = req.WorkflowStartDelay,
                    SignalName = input.Options.StartSignal,
                    WorkflowIdConflictPolicy = input.Options.IdConflictPolicy,
                    UserMetadata = req.UserMetadata,
                    Priority = req.Priority,
                    VersioningOverride = req.VersioningOverride,
                    Links = { req.Links },
                };
                if (input.Options.StartSignalArgs != null && input.Options.StartSignalArgs.Count > 0)
                {
                    signalReq.SignalInput = new Payloads();
                    signalReq.SignalInput.Payloads_.AddRange(
                        await dataConverter.ToPayloadsAsync(input.Options.StartSignalArgs).ConfigureAwait(false));
                }
                var signalResp = await Client.Connection.WorkflowService.SignalWithStartWorkflowExecutionAsync(
                    signalReq, DefaultRetryOptions(input.Options.Rpc)).ConfigureAwait(false);
                // Notice we do _not_ set first execution run ID for signal with start
                return new WorkflowHandle<TWorkflow, TResult>(
                    Client: Client,
                    Id: req.WorkflowId,
                    ResultRunId: signalResp.RunId);
            }

            private async Task<StartWorkflowExecutionRequest> CreateStartWorkflowRequestAsync(
                string workflow,
                IReadOnlyCollection<object?> args,
                WorkflowOptions options,
                DataConverter dataConverter,
                IDictionary<string, Payload>? headers)
            {
                var req = new StartWorkflowExecutionRequest()
                {
                    Namespace = Client.Options.Namespace,
                    WorkflowId = options.Id ??
                        throw new ArgumentException("ID required to start workflow"),
                    WorkflowType = new WorkflowType() { Name = workflow },
                    TaskQueue = new TaskQueue()
                    {
                        Name = options.TaskQueue ??
                            throw new ArgumentException("Task queue required to start workflow"),
                    },
                    Identity = Client.Connection.Options.Identity,
                    RequestId = options.RequestId ?? Guid.NewGuid().ToString(),
                    WorkflowIdReusePolicy = options.IdReusePolicy,
                    WorkflowIdConflictPolicy = options.IdConflictPolicy,
                    RetryPolicy = options.RetryPolicy?.ToProto(),
                    RequestEagerExecution = options.RequestEagerStart,
                    UserMetadata = await dataConverter.ToUserMetadataAsync(
                        options.StaticSummary, options.StaticDetails).
                        ConfigureAwait(false),
                    Priority = options.Priority?.ToProto(),
                    VersioningOverride = options.VersioningOverride?.ToProto(),
                    OnConflictOptions = options.OnConflictOptions,
                };
                if (args.Count > 0)
                {
                    req.Input = new Payloads();
                    req.Input.Payloads_.AddRange(await dataConverter.ToPayloadsAsync(
                        args).ConfigureAwait(false));
                }
                if (options.ExecutionTimeout != null)
                {
                    req.WorkflowExecutionTimeout = Duration.FromTimeSpan(
                        (TimeSpan)options.ExecutionTimeout);
                }
                if (options.RunTimeout != null)
                {
                    req.WorkflowRunTimeout = Duration.FromTimeSpan(
                        (TimeSpan)options.RunTimeout);
                }
                if (options.TaskTimeout != null)
                {
                    req.WorkflowTaskTimeout = Duration.FromTimeSpan(
                        (TimeSpan)options.TaskTimeout);
                }
                if (options.CronSchedule != null)
                {
                    req.CronSchedule = options.CronSchedule;
                }
                if (options.Memo != null && options.Memo.Count > 0)
                {
                    req.Memo = new();
                    foreach (var field in options.Memo)
                    {
                        if (field.Value == null)
                        {
                            throw new ArgumentException($"Memo value for {field.Key} is null");
                        }
                        req.Memo.Fields.Add(
                            field.Key,
                            await dataConverter.ToPayloadAsync(field.Value).ConfigureAwait(false));
                    }
                }
                if (options.TypedSearchAttributes != null && options.TypedSearchAttributes.Count > 0)
                {
                    req.SearchAttributes = options.TypedSearchAttributes.ToProto();
                }
                if (options.StartDelay is { } startDelay)
                {
                    req.WorkflowStartDelay = Duration.FromTimeSpan(startDelay);
                }
                if (headers != null)
                {
                    req.Header = new();
                    req.Header.Fields.Add(headers);
                    // If there is a payload codec, use it to encode the headers
                    if (dataConverter.PayloadCodec is IPayloadCodec codec)
                    {
                        foreach (var kvp in req.Header.Fields)
                        {
                            req.Header.Fields[kvp.Key] =
                                await codec.EncodeSingleAsync(kvp.Value).ConfigureAwait(false);
                        }
                    }
                }
                if (options.CompletionCallbacks is { } completionCallbacks)
                {
                    req.CompletionCallbacks.AddRange(completionCallbacks);
                }
                if (options.Links is { } links)
                {
                    req.Links.AddRange(links);
                }
                return req;
            }

            private async Task<UpdateWorkflowExecutionRequest> CreateUpdateWorkflowRequestAsync(
                string update,
                IReadOnlyCollection<object?> args,
                WorkflowUpdateOptions options,
                WorkflowUpdateStage waitForStage,
                DataConverter dataConverter,
                IDictionary<string, Payload>? headers)
            {
                if (waitForStage == WorkflowUpdateStage.None)
                {
                    throw new ArgumentException("WaitForStage is required to start workflow update");
                }
                else if (waitForStage == WorkflowUpdateStage.Admitted)
                {
                    throw new ArgumentException(
                        "Admitted is not an allowed wait stage to start workflow update");
                }
                // Build request
                var req = new UpdateWorkflowExecutionRequest()
                {
                    Namespace = Client.Options.Namespace,
                    Request = new()
                    {
                        Meta = new()
                        {
                            UpdateId = options.Id ?? Guid.NewGuid().ToString(),
                            Identity = Client.Connection.Options.Identity,
                        },
                        Input = new() { Name = update },
                    },
                    WaitPolicy = new()
                    {
                        LifecycleStage = (UpdateWorkflowExecutionLifecycleStage)waitForStage,
                    },
                };
                if (args.Count > 0)
                {
                    req.Request.Input.Args = new Payloads();
                    req.Request.Input.Args.Payloads_.AddRange(
                        await dataConverter.ToPayloadsAsync(args).ConfigureAwait(false));
                }
                if (headers != null)
                {
                    req.Request.Input.Header = new();
                    req.Request.Input.Header.Fields.Add(headers);
                    // If there is a payload codec, use it to encode the headers
                    if (dataConverter.PayloadCodec is IPayloadCodec codec)
                    {
                        foreach (var kvp in req.Request.Input.Header.Fields)
                        {
                            req.Request.Input.Header.Fields[kvp.Key] =
                                await codec.EncodeSingleAsync(kvp.Value).ConfigureAwait(false);
                        }
                    }
                }
                return req;
            }
        }
    }
}
