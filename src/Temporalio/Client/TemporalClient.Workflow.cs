using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Api.Common.V1;
using Temporalio.Api.TaskQueue.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;

#if NETCOREAPP3_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Temporalio.Client
{
    public partial class TemporalClient
    {
        /// <inheritdoc />
        public Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            Func<Task<TResult>> workflow, StartWorkflowOptions options)
        {
            return StartWorkflowAsync<TResult>(
                Workflow.WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[0],
                options);
        }

        /// <inheritdoc />
        public Task<WorkflowHandle<TResult>> StartWorkflowAsync<T, TResult>(
            Func<T, Task<TResult>> workflow, T arg, StartWorkflowOptions options)
        {
            return StartWorkflowAsync<TResult>(
                Workflow.WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);
        }

        /// <inheritdoc />
        public Task<WorkflowHandle> StartWorkflowAsync(
            Func<Task> workflow, StartWorkflowOptions options)
        {
            return StartWorkflowAsync(
                Workflow.WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[0],
                options);
        }

        /// <inheritdoc />
        public Task<WorkflowHandle> StartWorkflowAsync<T>(
            Func<T, Task> workflow, T arg, StartWorkflowOptions options)
        {
            return StartWorkflowAsync(
                Workflow.WorkflowDefinition.FromRunMethod(workflow.Method).Name,
                new object?[] { arg },
                options);
        }

        /// <inheritdoc />
        public async Task<WorkflowHandle> StartWorkflowAsync(
            string workflow, object?[] args, StartWorkflowOptions options)
        {
            return await StartWorkflowAsync<ValueTuple>(workflow, args, options);
        }

        /// <inheritdoc />
        public Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
            string workflow, object?[] args, StartWorkflowOptions options)
        {
            return OutboundInterceptor.StartWorkflowAsync<TResult>(new(
                Workflow: workflow,
                Args: args,
                Options: options,
                Headers: null));
        }

        /// <inheritdoc />
        public WorkflowHandle GetWorkflowHandle(
            string id, string? runID = null, string? firstExecutionRunID = null)
        {
            return new WorkflowHandle(
                Client: this, ID: id, RunID: runID, FirstExecutionRunID: firstExecutionRunID);
        }

        /// <inheritdoc />
        public WorkflowHandle<TResult> GetWorkflowHandle<TResult>(
            string id, string? runID = null, string? firstExecutionRunID = null)
        {
            return new WorkflowHandle<TResult>(
                Client: this, ID: id, RunID: runID, FirstExecutionRunID: firstExecutionRunID);
        }

#if NETCOREAPP3_0_OR_GREATER

        /// <inheritdoc />
        public IAsyncEnumerator<WorkflowExecution> ListWorkflows(
            string query, ListWorkflowsOptions? options = null)
        {
            throw new NotImplementedException();
        }

#endif

        internal partial class Impl
        {
            /// <inheritdoc />
            public override async Task<WorkflowHandle<TResult>> StartWorkflowAsync<TResult>(
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
                    req.Input.Payloads_.AddRange(
                        await Client.Options.DataConverter.ToPayloadsAsync(input.Args));
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
                    req.WorkflowExecutionTimeout = Duration.FromTimeSpan(
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
                        req.Memo.Fields.Add(
                            field.Key,
                            await Client.Options.DataConverter.ToPayloadAsync(field.Value));
                    }
                }
                if (input.Options.SearchAttributes != null && input.Options.SearchAttributes.Count > 0)
                {
                    req.SearchAttributes = DataConverter.Default.PayloadConverter.ToSearchAttributesProto(
                        input.Options.SearchAttributes);
                }
                if (input.Headers != null)
                {
                    req.Header = new();
                    req.Header.Fields.Add(input.Headers);
                }

                // If not signal with start, just run and return
                if (input.Options.StartSignal == null)
                {
                    if (input.Options.StartSignalArgs != null)
                    {
                        throw new ArgumentException("Cannot have start signal args without start signal");
                    }
                    var resp = await Client.Connection.WorkflowService.StartWorkflowExecutionAsync(
                        req, DefaultRetryOptions(input.Options.Rpc));
                    return new WorkflowHandle<TResult>(
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
                        await Client.Options.DataConverter.ToPayloadsAsync(input.Options.StartSignalArgs));
                }
                var signalResp = await Client.Connection.WorkflowService.SignalWithStartWorkflowExecutionAsync(
                    signalReq, DefaultRetryOptions(input.Options.Rpc));
                // Notice we do _not_ set first execution run ID for signal with start
                return new WorkflowHandle<TResult>(
                    Client: Client,
                    ID: req.WorkflowId,
                    ResultRunID: signalResp.RunId);
            }

            /// <inheritdoc />
            public override async Task<WorkflowHistoryEventPage> FetchWorkflowHistoryEventPage(
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
                        req, DefaultRetryOptions(input.Rpc));
                    // We don't support raw history
                    if (resp.RawHistory.Count > 0)
                    {
                        throw new InvalidOperationException("Unexpected raw history returned");
                    }
                    // Complete if we got any events or if there is no next page token
                    if (resp.History.Events.Count > 0 || resp.NextPageToken.IsEmpty)
                    {
                        return new WorkflowHistoryEventPage(
                            resp.History.Events,
                            resp.NextPageToken.IsEmpty ? null : resp.NextPageToken.ToByteArray());
                    }
                }
            }
        }
    }
}
