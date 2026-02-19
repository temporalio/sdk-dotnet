using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Temporalio.Api.Common.V1;
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
        public async Task<ActivityHandle<TResult>> StartActivityAsync<TResult>(
            Expression<Func<Task<TResult>>> activityCall, StartActivityOptions options)
        {
            var (method, args) = ExpressionUtil.ExtractCall(activityCall);
            return await OutboundInterceptor.StartActivityAsync<TResult>(new(
                Activity: Activities.ActivityDefinition.NameFromMethodForCall(method),
                Args: args,
                Options: options,
                Headers: null)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ActivityHandle> StartActivityAsync(
            Expression<Func<Task>> activityCall, StartActivityOptions options)
        {
            var (method, args) = ExpressionUtil.ExtractCall(activityCall);
            return await OutboundInterceptor.StartActivityAsync<ValueTuple>(new(
                Activity: Activities.ActivityDefinition.NameFromMethodForCall(method),
                Args: args,
                Options: options,
                Headers: null)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ActivityHandle> StartActivityAsync(
            string activity, IReadOnlyCollection<object?> args, StartActivityOptions options) =>
            await OutboundInterceptor.StartActivityAsync<ValueTuple>(new(
                Activity: activity,
                Args: args,
                Options: options,
                Headers: null)).ConfigureAwait(false);

        /// <inheritdoc />
        public ActivityHandle GetActivityHandle(string id, string? runId = null) =>
            new(Client: this, Id: id, RunId: runId);

        /// <inheritdoc />
        public ActivityHandle<TResult> GetActivityHandle<TResult>(string id, string? runId = null) =>
            new(Client: this, Id: id, RunId: runId);

#if NETCOREAPP3_0_OR_GREATER
        /// <inheritdoc />
        public IAsyncEnumerable<ActivityExecution> ListActivitiesAsync(
            string query, ActivityListOptions? options = null) =>
            OutboundInterceptor.ListActivitiesAsync(new(Query: query, Options: options));
#endif

        /// <inheritdoc />
        public Task<ActivityExecutionCount> CountActivitiesAsync(
            string query, ActivityCountOptions? options = null) =>
            OutboundInterceptor.CountActivitiesAsync(new(Query: query, Options: options));

        /// <inheritdoc />
        public Task<ActivityListPage> ListActivitiesPaginatedAsync(
            string query, byte[]? nextPageToken, ActivityListPaginatedOptions? options = null) =>
            OutboundInterceptor.ListActivitiesPaginatedAsync(new(Query: query, NextPageToken: nextPageToken, Options: options));

        internal partial class Impl
        {
            /// <inheritdoc />
            public override async Task<ActivityHandle<TResult>> StartActivityAsync<TResult>(
                StartActivityInput input)
            {
                if (input.Options.ScheduleToCloseTimeout == null && input.Options.StartToCloseTimeout == null)
                {
                    throw new ArgumentException(
                        "Activity must have ScheduleToCloseTimeout or StartToCloseTimeout");
                }
                try
                {
                    // Activity-specific data converter
                    var dataConverter = Client.Options.DataConverter.WithSerializationContext(
                        new ISerializationContext.Activity(
                            Namespace: Client.Options.Namespace,
                            ActivityId: input.Options.Id ?? throw new ArgumentException("ID required to start activity"),
                            WorkflowId: null,
                            WorkflowType: null,
                            ActivityType: input.Activity,
                            ActivityTaskQueue: input.Options.TaskQueue ?? throw new ArgumentException("Task queue required to start activity"),
                            IsLocal: false));

                    var req = new StartActivityExecutionRequest()
                    {
                        Namespace = Client.Options.Namespace,
                        ActivityId = input.Options.Id!,
                        ActivityType = new ActivityType() { Name = input.Activity },
                        TaskQueue = new TaskQueue()
                        {
                            Name = input.Options.TaskQueue!,
                        },
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                        IdReusePolicy = input.Options.IdReusePolicy,
                        IdConflictPolicy = input.Options.IdConflictPolicy,
                        RetryPolicy = input.Options.RetryPolicy?.ToProto(),
                        UserMetadata = await dataConverter.ToUserMetadataAsync(
                            input.Options.StaticSummary, input.Options.StaticDetails).
                            ConfigureAwait(false),
                        Priority = input.Options.Priority?.ToProto(),
                    };
                    if (input.Args.Count > 0)
                    {
                        req.Input = new Payloads();
                        req.Input.Payloads_.AddRange(
                            await dataConverter.ToPayloadsAsync(input.Args).ConfigureAwait(false));
                    }
                    if (input.Options.ScheduleToCloseTimeout is TimeSpan s2c)
                    {
                        req.ScheduleToCloseTimeout = Duration.FromTimeSpan(s2c);
                    }
                    if (input.Options.ScheduleToStartTimeout is TimeSpan s2s)
                    {
                        req.ScheduleToStartTimeout = Duration.FromTimeSpan(s2s);
                    }
                    if (input.Options.StartToCloseTimeout is TimeSpan s2close)
                    {
                        req.StartToCloseTimeout = Duration.FromTimeSpan(s2close);
                    }
                    if (input.Options.HeartbeatTimeout is TimeSpan hb)
                    {
                        req.HeartbeatTimeout = Duration.FromTimeSpan(hb);
                    }
                    if (input.Options.TypedSearchAttributes != null && input.Options.TypedSearchAttributes.Count > 0)
                    {
                        req.SearchAttributes = input.Options.TypedSearchAttributes.ToProto();
                    }
                    if (input.Headers != null)
                    {
                        req.Header = new();
                        req.Header.Fields.Add(input.Headers);
                        if (dataConverter.PayloadCodec is IPayloadCodec codec)
                        {
                            foreach (var kvp in req.Header.Fields)
                            {
                                req.Header.Fields[kvp.Key] =
                                    await codec.EncodeSingleAsync(kvp.Value).ConfigureAwait(false);
                            }
                        }
                    }

                    var resp = await Client.Connection.WorkflowService.StartActivityExecutionAsync(
                        req, DefaultRetryOptions(input.Options.Rpc)).ConfigureAwait(false);
                    return new ActivityHandle<TResult>(
                        Client: Client,
                        Id: input.Options.Id!,
                        RunId: resp.RunId);
                }
                catch (RpcException e) when (
                    e.Code == RpcException.StatusCode.AlreadyExists)
                {
                    var status = e.GrpcStatus.Value;
                    if (status != null && status.Details.Count == 1)
                    {
                        if (status.Details[0].TryUnpack(out Api.ErrorDetails.V1.ActivityExecutionAlreadyStartedFailure failure))
                        {
                            throw new ActivityAlreadyStartedException(
                                e.Message,
                                activityId: input.Options.Id!,
                                activityType: input.Activity,
                                runId: failure.RunId);
                        }
                    }
                    throw;
                }
            }

            /// <inheritdoc />
            public override async Task<ActivityExecutionDescription> DescribeActivityAsync(
                DescribeActivityInput input)
            {
                var resp = await Client.Connection.WorkflowService.DescribeActivityExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ActivityId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                return new(resp, Client.Options.Namespace, Client.Options.DataConverter);
            }

            /// <inheritdoc />
            public override async Task CancelActivityAsync(CancelActivityInput input)
            {
                await Client.Connection.WorkflowService.RequestCancelActivityExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ActivityId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                        Reason = input.Options?.Reason ?? string.Empty,
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task TerminateActivityAsync(TerminateActivityInput input)
            {
                await Client.Connection.WorkflowService.TerminateActivityExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        ActivityId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                        Reason = input.Options?.Reason ?? string.Empty,
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
            }

#if NETCOREAPP3_0_OR_GREATER
            /// <inheritdoc />
            public override IAsyncEnumerable<ActivityExecution> ListActivitiesAsync(
                ListActivitiesInput input) =>
                ListActivitiesInternalAsync(input);
#endif

            /// <inheritdoc />
            public override async Task<ActivityExecutionCount> CountActivitiesAsync(
                CountActivitiesInput input)
            {
                var resp = await Client.Connection.WorkflowService.CountActivityExecutionsAsync(
                    new() { Namespace = Client.Options.Namespace, Query = input.Query },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                return new(resp);
            }

            /// <inheritdoc />
            public override async Task<ActivityListPage> ListActivitiesPaginatedAsync(
                ListActivitiesPaginatedInput input)
            {
                var req = new ListActivityExecutionsRequest
                {
                    Namespace = Client.Options.Namespace,
                    PageSize = input.Options?.PageSize ?? 0,
                    Query = input.Query,
                };
                if (input.NextPageToken is not null)
                {
                    req.NextPageToken = ByteString.CopyFrom(input.NextPageToken);
                }

                var resp = await Client.Connection.WorkflowService.ListActivityExecutionsAsync(
                    req, DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);

                return new(
                    Activities: resp.Executions
                        .Select(e => new ActivityExecution(e, Client.Options.Namespace))
                        .ToList()
                        .AsReadOnly(),
                    NextPageToken: resp.NextPageToken.IsEmpty ? null : resp.NextPageToken.ToByteArray());
            }

#if NETCOREAPP3_0_OR_GREATER
            private async IAsyncEnumerable<ActivityExecution> ListActivitiesInternalAsync(
                ListActivitiesInput input,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                var limit = input.Options?.Limit ?? 0;
                if (limit < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(input), "Limit cannot be negative");
                }

                var rpcOptsAndCancelSource = DefaultRetryOptions(input.Options?.Rpc).
                    WithAdditionalCancellationToken(cancellationToken);
                try
                {
                    var pageOpts = new ActivityListPaginatedOptions { Rpc = rpcOptsAndCancelSource.Item1 };
                    byte[]? nextPageToken = null;
                    var yielded = 0;
                    do
                    {
                        var page = await Client.ListActivitiesPaginatedAsync(input.Query, nextPageToken, pageOpts).ConfigureAwait(false);
                        foreach (var exec in page.Activities)
                        {
                            yield return exec;
                            yielded++;
                            if (limit > 0 && yielded >= limit)
                            {
                                yield break;
                            }
                        }
                        nextPageToken = page.NextPageToken;
                    }
                    while (nextPageToken is not null);
                }
                finally
                {
                    rpcOptsAndCancelSource.Item2?.Dispose();
                }
            }
#endif
        }
    }
}
