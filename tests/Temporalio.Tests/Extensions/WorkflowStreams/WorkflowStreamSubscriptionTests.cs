namespace Temporalio.Tests.Extensions.WorkflowStreams;

using Temporalio.Api.Enums.V1;
using Temporalio.Api.Workflow.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Xunit;

public class WorkflowStreamSubscriptionTests
{
    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Canceled)]
    [InlineData(WorkflowExecutionStatus.Terminated)]
    [InlineData(WorkflowExecutionStatus.TimedOut)]
    public async Task SubscribeAsync_DrainsPinnedPagesForEveryTerminalStatus(
        WorkflowExecutionStatus status)
    {
        var transport = new StreamTransport { Status = status };
        await using var client = new WorkflowStreamClient(transport.Client, "workflow");
        var offsets = new List<long>();
        await foreach (var item in client.SubscribeAsync<string>(new() { FromOffset = 3 }))
        {
            offsets.Add(item.Offset);
            Assert.Equal("tail", item.Value);
        }
        Assert.Equal(new long[] { 3, 4 }, offsets);
        Assert.Equal(new[] { "closed-run", "closed-run" }, transport.QueryRuns);
        Assert.Equal(new long[] { 3, 4 }, transport.QueryOffsets);
    }

    [Fact]
    public async Task SubscribeAsync_AcceptedRunDoesNotLeakIntoLaterEnumerations()
    {
        var transport = new StreamTransport { AcceptPoll = true };
        await using var client = new WorkflowStreamClient(transport.Client, "workflow");
        var subscription = client.SubscribeAsync(new() { FromOffset = 3 });
        await foreach (var item in subscription)
        {
            Assert.InRange(item.Offset, 3, 4);
        }
        transport.AcceptPoll = false;
        await foreach (var item in subscription)
        {
            Assert.InRange(item.Offset, 3, 4);
        }
        Assert.Equal(new string?[] { "accepted-run", null }, transport.DescribedRuns);
    }

    [Fact]
    public async Task SubscribeAsync_RequiresTerminalRunId()
    {
        var transport = new StreamTransport { DescriptionRunId = string.Empty };
        await using var client = new WorkflowStreamClient(transport.Client, "workflow");
        await using var enumerator = client.SubscribeAsync().GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => enumerator.MoveNextAsync().AsTask());
        Assert.Same(transport.PollError, error);
        Assert.Empty(transport.QueryRuns);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SubscribeAsync_PropagatesQueryAndUnconfirmedClosureFailures(bool queryFails)
    {
        var transport = new StreamTransport
        {
            QueryError = queryFails ? new InvalidOperationException("query failed") : null,
            DescribeError = queryFails ? null : new InvalidOperationException("describe failed"),
        };
        await using var client = new WorkflowStreamClient(transport.Client, "workflow");
        await using var enumerator = client.SubscribeAsync().GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => enumerator.MoveNextAsync().AsTask());
        Assert.Same(queryFails ? transport.QueryError : transport.PollError, error);
    }

    private sealed class StreamTransport : IClientInterceptor
    {
        public StreamTransport()
        {
            Client = TemporalClient.CreateLazy(new("localhost:7233")
            {
                Interceptors = new[] { this },
            });
        }

        public ITemporalClient Client { get; }

        public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Completed;

        public string DescriptionRunId { get; set; } = "closed-run";

        public bool AcceptPoll { get; set; }

        public Exception PollError { get; } = new InvalidOperationException("closed");

        public Exception? QueryError { get; set; }

        public Exception? DescribeError { get; set; }

        public List<string?> QueryRuns { get; } = new();

        public List<long> QueryOffsets { get; } = new();

        public List<string?> DescribedRuns { get; } = new();

        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor nextInterceptor) =>
            new Outbound(nextInterceptor, this);

        private sealed class Outbound : ClientOutboundInterceptor
        {
            private readonly StreamTransport transport;

            public Outbound(ClientOutboundInterceptor next, StreamTransport transport)
                : base(next) => this.transport = transport;

            public override Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
                StartWorkflowUpdateInput input) =>
                transport.AcceptPoll ?
                    Task.FromResult<WorkflowUpdateHandle<TResult>>(
                        new FailedUpdateHandle<TResult>(transport.Client)) :
                    Task.FromException<WorkflowUpdateHandle<TResult>>(transport.PollError);

            public override Task<WorkflowExecutionDescription> DescribeWorkflowAsync(
                DescribeWorkflowInput input)
            {
                transport.DescribedRuns.Add(input.RunId);
                if (transport.DescribeError is { } error)
                {
                    return Task.FromException<WorkflowExecutionDescription>(error);
                }
                return Task.FromResult(new WorkflowExecutionDescription(
                    new DescribeWorkflowExecutionResponse
                    {
                        WorkflowExecutionInfo = new WorkflowExecutionInfo
                        {
                            Execution = new() { WorkflowId = "workflow", RunId = transport.DescriptionRunId },
                            Status = transport.Status,
                        },
                    },
                    DataConverter.Default));
            }

            public override Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
            {
                if (transport.QueryError is { } error)
                {
                    return Task.FromException<TResult>(error);
                }
                Assert.Equal(WorkflowStreamConstants.PollQueryName, input.Query);
                Assert.NotNull(input.Options?.Rpc?.CancellationToken);
                Assert.Equal(TimeSpan.FromSeconds(30), input.Options?.Rpc?.Timeout);
                var request = Assert.IsType<PollInput>(Assert.Single(input.Args));
                transport.QueryRuns.Add(input.RunId);
                transport.QueryOffsets.Add(request.FromOffset);
                var result = new PollResult
                {
                    Items = new[]
                    {
                        new WorkflowStreamWireItem
                        {
                            Topic = "events",
                            Data = PayloadWire.Encode(DataConverter.Default.PayloadConverter.ToPayload("tail")),
                            Offset = request.FromOffset,
                        },
                    },
                    NextOffset = request.FromOffset + 1,
                    MoreReady = request.FromOffset == 3,
                };
                return Task.FromResult((TResult)(object)result);
            }
        }

        private sealed record FailedUpdateHandle<TResult>(ITemporalClient Client) :
            WorkflowUpdateHandle<TResult>(Client, "update", "workflow", "accepted-run")
        {
            public override Task<T> GetResultAsync<T>(RpcOptions? rpcOptions = null) =>
                Task.FromException<T>(new InvalidOperationException("closed during accepted poll"));
        }
    }
}
