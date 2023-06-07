using System;
using System.Collections.Generic;
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
                Workflow: Workflows.WorkflowDefinition.FromRunMethod(runMethod).Name,
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
                Workflow: Workflows.WorkflowDefinition.FromRunMethod(runMethod).Name,
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
            string id, string? runID = null, string? firstExecutionRunID = null) =>
            new(Client: this, ID: id, RunID: runID, FirstExecutionRunID: firstExecutionRunID);

        /// <inheritdoc />
        public WorkflowHandle<TWorkflow> GetWorkflowHandle<TWorkflow>(
            string id, string? runID = null, string? firstExecutionRunID = null) =>
            new(Client: this, ID: id, RunID: runID, FirstExecutionRunID: firstExecutionRunID);

        /// <inheritdoc />
        public WorkflowHandle<TWorkflow, TResult> GetWorkflowHandle<TWorkflow, TResult>(
            string id, string? runID = null, string? firstExecutionRunID = null) =>
            new(Client: this, ID: id, RunID: runID, FirstExecutionRunID: firstExecutionRunID);

#if NETCOREAPP3_0_OR_GREATER
        /// <inheritdoc />
        public IAsyncEnumerable<WorkflowExecution> ListWorkflowsAsync(
            string query, WorkflowListOptions? options = null) =>
            OutboundInterceptor.ListWorkflowsAsync(new(Query: query, Options: options));
#endif

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
                                input.Options.ID!,
                                failure.RunId);
                        }
                    }
                    throw;
                }
            }

            /// <inheritdoc />
            public override async Task SignalWorkflowAsync(SignalWorkflowInput input)
            {
                var req = new SignalWorkflowExecutionRequest()
                {
                    Namespace = Client.Options.Namespace,
                    WorkflowExecution = new()
                    {
                        WorkflowId = input.ID,
                        RunId = input.RunID ?? string.Empty,
                    },
                    SignalName = input.Signal,
                    Identity = Client.Connection.Options.Identity,
                    RequestId = Guid.NewGuid().ToString(),
                };
                if (input.Args.Count > 0)
                {
                    req.Input = new Payloads();
                    req.Input.Payloads_.AddRange(await Client.Options.DataConverter.ToPayloadsAsync(
                        input.Args).ConfigureAwait(false));
                }
                if (input.Headers != null)
                {
                    req.Header = new();
                    req.Header.Fields.Add(input.Headers);
                    // If there is a payload codec, use it to encode the headers
                    if (Client.Options.DataConverter.PayloadCodec is IPayloadCodec codec)
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
                var req = new QueryWorkflowRequest()
                {
                    Namespace = Client.Options.Namespace,
                    Execution = new()
                    {
                        WorkflowId = input.ID,
                        RunId = input.RunID ?? string.Empty,
                    },
                    Query = new() { QueryType = input.Query },
                };
                if (input.Args.Count > 0)
                {
                    req.Query.QueryArgs = new Payloads();
                    req.Query.QueryArgs.Payloads_.AddRange(
                        await Client.Options.DataConverter.ToPayloadsAsync(
                            input.Args).ConfigureAwait(false));
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
                    if (Client.Options.DataConverter.PayloadCodec is IPayloadCodec codec)
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

                if (resp.QueryResult == null)
                {
                    throw new InvalidOperationException("No result present");
                }
                return await Client.Options.DataConverter.ToSingleValueAsync<TResult>(
                    resp.QueryResult.Payloads_).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task<WorkflowExecutionDescription> DescribeWorkflowAsync(
                DescribeWorkflowInput input)
            {
                var resp = await Client.Connection.WorkflowService.DescribeWorkflowExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        Execution = new()
                        {
                            WorkflowId = input.ID,
                            RunId = input.RunID ?? string.Empty,
                        },
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                return new(resp, Client.Options.DataConverter);
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
                            WorkflowId = input.ID,
                            RunId = input.RunID ?? string.Empty,
                        },
                        FirstExecutionRunId = input.FirstExecutionRunID ?? string.Empty,
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
                        WorkflowId = input.ID,
                        RunId = input.RunID ?? string.Empty,
                    },
                    Reason = input.Reason ?? string.Empty,
                    FirstExecutionRunId = input.FirstExecutionRunID ?? string.Empty,
                    Identity = Client.Connection.Options.Identity,
                };
                if (input.Options?.Details != null && input.Options?.Details.Count > 0)
                {
                    req.Details = new Payloads();
                    req.Details.Payloads_.AddRange(
                        await Client.Options.DataConverter.ToPayloadsAsync(
                            input.Options.Details).ConfigureAwait(false));
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
                        WorkflowId = input.ID,
                        RunId = input.RunID ?? string.Empty,
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

            private async IAsyncEnumerable<WorkflowExecution> ListWorkflowsInternalAsync(
                ListWorkflowsInput input,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                // Need to combine cancellation token
                var rpcOptsAndCancelSource = DefaultRetryOptions(input.Options?.Rpc).
                    WithAdditionalCancellationToken(cancellationToken);
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
                            yield return new(exec, Client.Options.DataConverter);
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
                // We will build the non-signal-with-start request and convert to signal with start
                // later if needed
                var req = new StartWorkflowExecutionRequest()
                {
                    Namespace = Client.Options.Namespace,
                    WorkflowId = input.Options.ID ??
                        throw new ArgumentException("ID required to start workflow"),
                    WorkflowType = new WorkflowType() { Name = input.Workflow },
                    TaskQueue = new TaskQueue()
                    {
                        Name = input.Options.TaskQueue ??
                            throw new ArgumentException("Task queue required to start workflow"),
                    },
                    Identity = Client.Connection.Options.Identity,
                    RequestId = Guid.NewGuid().ToString(),
                    WorkflowIdReusePolicy = input.Options.IDReusePolicy,
                    RetryPolicy = input.Options.RetryPolicy?.ToProto(),
                };
                if (input.Args.Count > 0)
                {
                    req.Input = new Payloads();
                    req.Input.Payloads_.AddRange(await Client.Options.DataConverter.ToPayloadsAsync(
                        input.Args).ConfigureAwait(false));
                }
                if (input.Options.ExecutionTimeout != null)
                {
                    req.WorkflowExecutionTimeout = Duration.FromTimeSpan(
                        (TimeSpan)input.Options.ExecutionTimeout);
                }
                if (input.Options.RunTimeout != null)
                {
                    req.WorkflowRunTimeout = Duration.FromTimeSpan(
                        (TimeSpan)input.Options.RunTimeout);
                }
                if (input.Options.TaskTimeout != null)
                {
                    req.WorkflowTaskTimeout = Duration.FromTimeSpan(
                        (TimeSpan)input.Options.TaskTimeout);
                }
                if (input.Options.CronSchedule != null)
                {
                    req.CronSchedule = input.Options.CronSchedule;
                }
                if (input.Options.Memo != null && input.Options.Memo.Count > 0)
                {
                    req.Memo = new();
                    foreach (var field in input.Options.Memo)
                    {
                        if (field.Value == null)
                        {
                            throw new ArgumentException($"Memo value for {field.Key} is null");
                        }
                        req.Memo.Fields.Add(
                            field.Key,
                            await Client.Options.DataConverter.ToPayloadAsync(field.Value).ConfigureAwait(false));
                    }
                }
                if (input.Options.TypedSearchAttributes != null && input.Options.TypedSearchAttributes.Count > 0)
                {
                    req.SearchAttributes = input.Options.TypedSearchAttributes.ToProto();
                }
                if (input.Headers != null)
                {
                    req.Header = new();
                    req.Header.Fields.Add(input.Headers);
                    // If there is a payload codec, use it to encode the headers
                    if (Client.Options.DataConverter.PayloadCodec is IPayloadCodec codec)
                    {
                        foreach (var kvp in req.Header.Fields)
                        {
                            req.Header.Fields[kvp.Key] =
                                await codec.EncodeSingleAsync(kvp.Value).ConfigureAwait(false);
                        }
                    }
                }

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
                        ID: req.WorkflowId,
                        ResultRunID: resp.RunId,
                        FirstExecutionRunID: resp.RunId);
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
                    SignalName = input.Options.StartSignal,
                };
                if (input.Options.StartSignalArgs != null && input.Options.StartSignalArgs.Count > 0)
                {
                    signalReq.SignalInput = new Payloads();
                    signalReq.SignalInput.Payloads_.AddRange(
                        await Client.Options.DataConverter.ToPayloadsAsync(
                            input.Options.StartSignalArgs).ConfigureAwait(false));
                }
                var signalResp = await Client.Connection.WorkflowService.SignalWithStartWorkflowExecutionAsync(
                    signalReq, DefaultRetryOptions(input.Options.Rpc)).ConfigureAwait(false);
                // Notice we do _not_ set first execution run ID for signal with start
                return new WorkflowHandle<TWorkflow, TResult>(
                    Client: Client,
                    ID: req.WorkflowId,
                    ResultRunID: signalResp.RunId);
            }
        }
    }
}
