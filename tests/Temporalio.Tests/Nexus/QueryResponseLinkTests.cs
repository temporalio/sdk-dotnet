namespace Temporalio.Tests.Nexus;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using NexusRpc.Handlers;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.Query.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Nexus;
using Xunit;

/// <summary>
/// Unit tests for query response link propagation in and out of the Nexus operation context. These
/// run against a fake workflow service, covering behavior the end-to-end tests cannot reach until
/// the server populates <c>QueryWorkflowResponse.Link</c>.
/// </summary>
public class QueryResponseLinkTests
{
    [Fact]
    public async Task QueryAsync_CapturesWorkflowResponseLink()
    {
        // A query never writes to history, so the server answers with a Link.Workflow naming the
        // execution that processed it instead of a Link.WorkflowEvent. That link has to reach the
        // operation context so the caller's Nexus operation event points back at the queried
        // workflow.
        var responseLink = WorkflowLink("wf-target", "target-run", "Query processed");
        var client = NewClient(new QueryWorkflowResponse
        {
            Link = responseLink,
            QueryResult = await ToPayloadsAsync("answer"),
        });
        var context = NewContext();

        var result = await WithContextAsync(
            context, () => QueryAsync<string>(client));

        // Capturing the link must not disturb the query's own result.
        Assert.Equal("answer", result);
        Assert.Equal(new[] { responseLink }, context.ResponseLinks);
    }

    [Fact]
    public async Task QueryAsync_AgainstOlderServerCapturesNoResponseLink()
    {
        // Older servers leave the field unset, so nothing is captured and the query still succeeds.
        var client = NewClient(new QueryWorkflowResponse
        {
            QueryResult = await ToPayloadsAsync("answer"),
        });
        var context = NewContext();

        var result = await WithContextAsync(
            context, () => QueryAsync<string>(client));

        Assert.Equal("answer", result);
        Assert.Empty(context.ResponseLinks);
    }

    [Fact]
    public async Task QueryAsync_OutsideNexusContextIgnoresResponseLink()
    {
        // A query issued outside a Nexus operation handler must not touch the operation context at
        // all. Guards against the propagation being reached without a context, which would throw.
        var client = NewClient(new QueryWorkflowResponse
        {
            Link = WorkflowLink("wf-target", "target-run", "Query processed"),
            QueryResult = await ToPayloadsAsync("answer"),
        });
        var context = NewContext();

        // Deliberately not inside WithContextAsync.
        var result = await QueryAsync<string>(client);

        Assert.Equal("answer", result);
        Assert.Empty(context.ResponseLinks);
    }

    [Fact]
    public async Task QueryAsync_MultipleQueriesAccumulateAllResponseLinks()
    {
        // Two queries in a row each contribute a response link; both must accumulate in call order
        // on the shared list, exactly as the signal path does.
        var first = WorkflowLink("callee-a", "run-a", "Query processed");
        var second = WorkflowLink("callee-b", "run-b", "Query processed");
        var payloads = await ToPayloadsAsync("answer");
        var client = NewClient(
            new QueryWorkflowResponse { Link = first, QueryResult = payloads },
            new QueryWorkflowResponse { Link = second, QueryResult = payloads });
        var context = NewContext();

        await WithContextAsync(context, async () =>
        {
            await QueryAsync<string>(client);
            await QueryAsync<string>(client);
            return 0;
        });

        Assert.Equal(new[] { first, second }, context.ResponseLinks);
    }

    [Fact]
    public async Task QueryAsync_RejectedQueryStillCapturesResponseLink()
    {
        // A rejected query still carries a link to the workflow that rejected it, and the link is
        // captured before the rejection is surfaced. Pins the ordering so it is not "fixed" into the
        // wrong behavior later.
        var responseLink = WorkflowLink("wf-target", "target-run", "Query processed");
        var client = NewClient(new QueryWorkflowResponse
        {
            Link = responseLink,
            QueryRejected = new() { Status = WorkflowExecutionStatus.Completed },
        });
        var context = NewContext();

        await Assert.ThrowsAsync<WorkflowQueryRejectedException>(
            () => WithContextAsync(context, () => QueryAsync<string>(client)));

        Assert.Equal(new[] { responseLink }, context.ResponseLinks);
    }

    private static Task<TResult> QueryAsync<TResult>(TemporalClient client) =>
        client.GetWorkflowHandle("wf-target").QueryAsync<TResult>("test-query", Array.Empty<object?>());

    private static async Task<T> WithContextAsync<T>(
        NexusOperationExecutionContext context, Func<Task<T>> func)
    {
        NexusOperationExecutionContext.AsyncLocalCurrent.Value = context;
        try
        {
            return await func().ConfigureAwait(false);
        }
        finally
        {
            NexusOperationExecutionContext.AsyncLocalCurrent.Value = null;
        }
    }

    private static Task<Payloads> ToPayloadsAsync(object value) =>
        Task.FromResult(new Payloads
        {
            Payloads_ = { Temporalio.Converters.DataConverter.Default.PayloadConverter.ToPayload(value) },
        });

    private static TemporalClient NewClient(params QueryWorkflowResponse[] responses) =>
        new TemporalClient(
            new FakeConnection(new FakeWorkflowService(responses)),
            new TemporalClientOptions { Namespace = "test-namespace" });

    private static NexusOperationExecutionContext NewContext()
    {
        var handlerContext = new OperationStartContext(
            Service: "svc",
            Operation: "op",
            CancellationToken: CancellationToken.None,
            RequestId: Guid.NewGuid().ToString());
        return new NexusOperationExecutionContext(
            handlerContext: handlerContext,
            info: new("test-namespace", "tq", "endpoint"),
            logger: NullLogger.Instance,
            runtimeMetricMeter: new Lazy<MetricMeter>(
                () => throw new InvalidOperationException("metric meter not expected in test")),
            temporalClient: null);
    }

    private static Link WorkflowLink(string workflowId, string runId, string reason) =>
        new()
        {
            Workflow = new()
            {
                Namespace = "test-namespace",
                WorkflowId = workflowId,
                RunId = runId,
                Reason = reason,
            },
        };

    /// <summary>Workflow service that replays canned responses in order.</summary>
    private class FakeWorkflowService : WorkflowService
    {
        private readonly Queue<QueryWorkflowResponse> responses;

        public FakeWorkflowService(IEnumerable<QueryWorkflowResponse> responses) =>
            this.responses = new(responses);

        internal override Bridge.Interop.TemporalCoreRpcService Service =>
            Bridge.Interop.TemporalCoreRpcService.Workflow;

        internal override string FullName => "temporal.api.workflowservice.v1.WorkflowService";

        protected override Task<T> InvokeRpcAsync<T>(
            string rpc, IMessage req, MessageParser<T> resp, RpcOptions? options = null)
        {
            if (rpc != "QueryWorkflow")
            {
                throw new NotSupportedException($"Unexpected RPC: {rpc}");
            }
            return Task.FromResult((T)(object)responses.Dequeue());
        }
    }

    /// <summary>Connection that exposes only the fake workflow service.</summary>
    private class FakeConnection : ITemporalConnection
    {
        private readonly WorkflowService workflowService;

        public FakeConnection(WorkflowService workflowService) =>
            this.workflowService = workflowService;

        public string? ApiKey { get; set; }

        public IReadOnlyCollection<KeyValuePair<string, string>> RpcMetadata { get; set; } =
            Array.Empty<KeyValuePair<string, string>>();

        public IReadOnlyCollection<KeyValuePair<string, byte[]>> RpcBinaryMetadata { get; set; } =
            Array.Empty<KeyValuePair<string, byte[]>>();

        public WorkflowService WorkflowService => workflowService;

        public OperatorService OperatorService => throw new NotSupportedException();

        public CloudService CloudService => throw new NotSupportedException();

        public TestService TestService => throw new NotSupportedException();

        public TemporalConnectionOptions Options => new();

        public bool IsConnected => true;

        public SafeHandle? BridgeClient => null;

        public Task<bool> CheckHealthAsync(
            RpcService? service = null, RpcOptions? options = null) =>
            throw new NotSupportedException();

        public Task ConnectAsync() => throw new NotSupportedException();
    }
}
