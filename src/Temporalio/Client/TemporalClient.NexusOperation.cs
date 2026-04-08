using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NexusRpc;
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
        public NexusClient CreateNexusClient(string service, NexusClientOptions options) =>
            new NexusClientImpl(
                client: this,
                service: service,
                endpoint: options.Endpoint ?? throw new ArgumentException("Endpoint is required"));

        /// <inheritdoc />
        public NexusClient<TService> CreateNexusClient<TService>(NexusClientOptions options) =>
            new NexusClientImpl<TService>(
                client: this,
                endpoint: options.Endpoint ?? throw new ArgumentException("Endpoint is required"));

        /// <inheritdoc />
        public NexusClient<TService> CreateNexusClient<TService>(string endpoint) =>
            new NexusClientImpl<TService>(client: this, endpoint: endpoint);

        /// <inheritdoc />
        public NexusOperationHandle GetNexusOperationHandle(string operationId, string? operationRunId = null) =>
            new(Client: this, Id: operationId, RunId: operationRunId);

        /// <inheritdoc />
        public NexusOperationHandle<TResult> GetNexusOperationHandle<TResult>(string operationId, string? operationRunId = null) =>
            new(Client: this, Id: operationId, RunId: operationRunId);

#if NETCOREAPP3_0_OR_GREATER
        /// <inheritdoc />
        public IAsyncEnumerable<NexusOperationExecution> ListNexusOperationsAsync(
            string query, NexusOperationListOptions? options = null) =>
            OutboundInterceptor.ListNexusOperationsAsync(new(Query: query, Options: options));
#endif

        /// <inheritdoc />
        public Task<NexusOperationExecutionCount> CountNexusOperationsAsync(
            string query, NexusOperationCountOptions? options = null) =>
            OutboundInterceptor.CountNexusOperationsAsync(new(Query: query, Options: options));

        /// <inheritdoc />
        public Task<NexusOperationListPage> ListNexusOperationsPaginatedAsync(
            string query, byte[]? nextPageToken, NexusOperationListPaginatedOptions? options = null) =>
            OutboundInterceptor.ListNexusOperationsPaginatedAsync(new(Query: query, NextPageToken: nextPageToken, Options: options));

        internal partial class Impl
        {
            /// <inheritdoc />
            public override async Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
                StartNexusOperationInput input)
            {
                try
                {
                    var dataConverter = Client.Options.DataConverter;

                    var req = new StartNexusOperationExecutionRequest()
                    {
                        Namespace = Client.Options.Namespace,
                        OperationId = input.Options.Id ?? throw new ArgumentException("ID required to start Nexus operation"),
                        Endpoint = input.Endpoint,
                        Service = input.Service,
                        Operation = input.Operation,
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                        IdReusePolicy = input.Options.IdReusePolicy,
                        IdConflictPolicy = input.Options.IdConflictPolicy,
                        UserMetadata = await dataConverter.ToUserMetadataAsync(
                            input.Options.Summary, null).ConfigureAwait(false),
                    };
                    if (input.Arg != null)
                    {
                        req.Input = await dataConverter.ToPayloadAsync(input.Arg).ConfigureAwait(false);
                    }
                    if (input.Options.ScheduleToCloseTimeout is TimeSpan s2c)
                    {
                        req.ScheduleToCloseTimeout = Duration.FromTimeSpan(s2c);
                    }
                    if (input.Options.SearchAttributes != null && input.Options.SearchAttributes.Count > 0)
                    {
                        req.SearchAttributes = input.Options.SearchAttributes.ToProto();
                    }
                    if (input.Headers != null)
                    {
                        req.NexusHeader.Add(
                            input.Headers.ToDictionary(
                                kvp => kvp.Key,
                                kvp => System.Text.Encoding.UTF8.GetString(kvp.Value.Data.ToByteArray())));
                    }

                    var resp = await Client.Connection.WorkflowService.StartNexusOperationExecutionAsync(
                        req, DefaultRetryOptions(input.Options.Rpc)).ConfigureAwait(false);
                    return new NexusOperationHandle<TResult>(
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
                        if (status.Details[0].TryUnpack(out Api.ErrorDetails.V1.NexusOperationExecutionAlreadyStartedFailure failure))
                        {
                            throw new NexusOperationAlreadyStartedException(
                                e.Message,
                                operationId: input.Options.Id!,
                                runId: failure.RunId);
                        }
                    }
                    throw;
                }
            }

            /// <inheritdoc />
            public override async Task<NexusOperationExecutionDescription> DescribeNexusOperationAsync(
                DescribeNexusOperationInput input)
            {
                var req = new DescribeNexusOperationExecutionRequest()
                {
                    Namespace = Client.Options.Namespace,
                    OperationId = input.Id,
                    RunId = input.RunId ?? string.Empty,
                };
                if (input.Options?.LongPollToken is byte[] token)
                {
                    req.LongPollToken = ByteString.CopyFrom(token);
                }
                var resp = await Client.Connection.WorkflowService.DescribeNexusOperationExecutionAsync(
                    req, DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                return new(resp, Client.Options.Namespace, Client.Options.DataConverter);
            }

            /// <inheritdoc />
            public override async Task CancelNexusOperationAsync(CancelNexusOperationInput input)
            {
                await Client.Connection.WorkflowService.RequestCancelNexusOperationExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        OperationId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                        Reason = input.Options?.Reason ?? string.Empty,
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
            }

            /// <inheritdoc />
            public override async Task TerminateNexusOperationAsync(TerminateNexusOperationInput input)
            {
                await Client.Connection.WorkflowService.TerminateNexusOperationExecutionAsync(
                    new()
                    {
                        Namespace = Client.Options.Namespace,
                        OperationId = input.Id,
                        RunId = input.RunId ?? string.Empty,
                        Identity = Client.Connection.Options.Identity,
                        RequestId = Guid.NewGuid().ToString(),
                        Reason = input.Options?.Reason ?? string.Empty,
                    },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
            }

#if NETCOREAPP3_0_OR_GREATER
            /// <inheritdoc />
            public override IAsyncEnumerable<NexusOperationExecution> ListNexusOperationsAsync(
                ListNexusOperationsInput input) =>
                ListNexusOperationsInternalAsync(input);
#endif

            /// <inheritdoc />
            public override async Task<NexusOperationExecutionCount> CountNexusOperationsAsync(
                CountNexusOperationsInput input)
            {
                var resp = await Client.Connection.WorkflowService.CountNexusOperationExecutionsAsync(
                    new() { Namespace = Client.Options.Namespace, Query = input.Query },
                    DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);
                return new(resp);
            }

            /// <inheritdoc />
            public override async Task<NexusOperationListPage> ListNexusOperationsPaginatedAsync(
                ListNexusOperationsPaginatedInput input)
            {
                var req = new ListNexusOperationExecutionsRequest
                {
                    Namespace = Client.Options.Namespace,
                    PageSize = input.Options?.PageSize ?? 0,
                    Query = input.Query,
                };
                if (input.NextPageToken is not null)
                {
                    req.NextPageToken = ByteString.CopyFrom(input.NextPageToken);
                }

                var resp = await Client.Connection.WorkflowService.ListNexusOperationExecutionsAsync(
                    req, DefaultRetryOptions(input.Options?.Rpc)).ConfigureAwait(false);

                return new(
                    Operations: resp.Operations
                        .Select(e => new NexusOperationExecution(e, Client.Options.Namespace))
                        .ToList()
                        .AsReadOnly(),
                    NextPageToken: resp.NextPageToken.IsEmpty ? null : resp.NextPageToken.ToByteArray());
            }

#if NETCOREAPP3_0_OR_GREATER
            private async IAsyncEnumerable<NexusOperationExecution> ListNexusOperationsInternalAsync(
                ListNexusOperationsInput input,
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
                    var pageOpts = new NexusOperationListPaginatedOptions { Rpc = rpcOptsAndCancelSource.Item1 };
                    byte[]? nextPageToken = null;
                    var yielded = 0;
                    do
                    {
                        var page = await Client.ListNexusOperationsPaginatedAsync(input.Query, nextPageToken, pageOpts).ConfigureAwait(false);
                        foreach (var exec in page.Operations)
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

        private sealed class NexusClientImpl : NexusClient
        {
            private readonly TemporalClient client;

            internal NexusClientImpl(TemporalClient client, string service, string endpoint)
            {
                this.client = client;
                Service = service;
                Endpoint = endpoint;
            }

            public override string Service { get; }

            public override string Endpoint { get; }

            public override ITemporalClient Client => client;

            public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
                string operationName, object? arg, NexusOperationOptions? options = null) =>
                client.OutboundInterceptor.StartNexusOperationAsync<TResult>(new(
                    Service: Service,
                    Endpoint: Endpoint,
                    Operation: operationName,
                    Arg: arg,
                    Options: options ?? new NexusOperationOptions(),
                    Headers: null));
        }

        private sealed class NexusClientImpl<TService> : NexusClient<TService>
        {
            private readonly TemporalClient client;

            internal NexusClientImpl(TemporalClient client, string endpoint)
            {
                this.client = client;
                Endpoint = endpoint;
                ServiceDefinition = ServiceDefinition.FromType<TService>();
            }

            public override string Endpoint { get; }

            public override ITemporalClient Client => client;

            public override ServiceDefinition ServiceDefinition { get; }

            public override Task<NexusOperationHandle<TResult>> StartNexusOperationAsync<TResult>(
                string operationName, object? arg, NexusOperationOptions? options = null) =>
                client.OutboundInterceptor.StartNexusOperationAsync<TResult>(new(
                    Service: Service,
                    Endpoint: Endpoint,
                    Operation: operationName,
                    Arg: arg,
                    Options: options ?? new NexusOperationOptions(),
                    Headers: null));
        }
    }
}
