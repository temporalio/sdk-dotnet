namespace Temporalio.Tests.Extensions.WorkflowStreams;

using Temporalio.Converters;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowStreamTests : WorkflowEnvironmentTestBase
{
    public WorkflowStreamTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task PublishSubscribe_IsReusableFilteredAndCancelable()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await using var client = new WorkflowStreamClient(
                Client,
                handle.Id,
                new() { BatchInterval = TimeSpan.FromHours(1) });
            var orders = client.Topic("orders").SubscribeAsync();

            client.Topic("orders").Publish("first");
            client.Topic("audit").Publish("ignored");
            await client.FlushAsync(default);

            await using var first = orders.GetAsyncEnumerator();
            await using var second = orders.GetAsyncEnumerator();
            Assert.True(await first.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.True(await second.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal("first", DecodeString(first.Current));
            Assert.Equal("first", DecodeString(second.Current));
            Assert.Equal(0, first.Current.Offset);
            Assert.Equal(0, second.Current.Offset);

            using var cancellationSource = new CancellationTokenSource();
            await using var canceled = client.SubscribeAsync(new() { FromOffset = 2 }).
                WithCancellation(cancellationSource.Token).GetAsyncEnumerator();
            var canceledMove = Task.Run(async () => await canceled.MoveNextAsync());
            await cancellationSource.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => canceledMove.WaitAsync(TimeSpan.FromSeconds(15)));

            var disposedClient = new WorkflowStreamClient(Client, handle.Id);
            await using var disposedEnumerator = disposedClient.SubscribeAsync(
                new() { FromOffset = 2 }).GetAsyncEnumerator();
            var disposedMove = disposedEnumerator.MoveNextAsync().AsTask();
            await disposedClient.DisposeAsync();
            Assert.False(await disposedMove.WaitAsync(TimeSpan.FromSeconds(15)));

            await using var terminal = client.SubscribeAsync(
                new() { FromOffset = 2 }).GetAsyncEnumerator();
            var terminalMove = terminal.MoveNextAsync().AsTask();
            await handle.SignalAsync(workflow => workflow.FinishAsync());
            Assert.False(await terminalMove.WaitAsync(TimeSpan.FromSeconds(15)));
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Truncate_ResetsFallenBehindSubscriptionToRetainedBeginning()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await using var client = new WorkflowStreamClient(
                Client,
                handle.Id,
                new() { BatchInterval = TimeSpan.FromHours(1) });
            client.Topic(string.Empty).Publish("zero");
            client.Topic(string.Empty).Publish("one");
            client.Topic(string.Empty).Publish("two");
            await client.FlushAsync(default);
            Assert.Equal(3, await client.GetOffsetAsync(default));

            await handle.SignalAsync(workflow => workflow.TruncateAsync(2));
            Assert.Equal(2, await handle.QueryAsync(workflow => workflow.BaseOffset()));
            await using var enumerator = client.SubscribeAsync(
                new() { FromOffset = 1 }).GetAsyncEnumerator();
            Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal("two", DecodeString(enumerator.Current));
            Assert.Equal(2, enumerator.Current.Offset);

            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task PublishSignal_DeduplicatesAndSkipsMalformedEntries()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var converter = DataConverter.Default.PayloadConverter;
            var first = new PublishInput
            {
                PublisherId = "publisher",
                Sequence = 1,
                Items = new[]
                {
                    new PublishEntry
                    {
                        Topic = "events",
                        Data = PayloadWire.Encode(converter.ToPayload("accepted")),
                    },
                },
            };
            var duplicate = new PublishInput
            {
                PublisherId = "publisher",
                Sequence = 1,
                Items = new[]
                {
                    new PublishEntry
                    {
                        Topic = "events",
                        Data = PayloadWire.Encode(converter.ToPayload("duplicate")),
                    },
                },
            };
            var malformed = new PublishInput
            {
                PublisherId = "publisher",
                Sequence = 2,
                Items = new[]
                {
                    new PublishEntry { Topic = "events", Data = "not base64" },
                },
            };

            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName, new object?[] { first });
            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName, new object?[] { duplicate });
            await handle.SignalAsync(
                WorkflowStreamConstants.PublishSignalName, new object?[] { malformed });
            await using var client = new WorkflowStreamClient(Client, handle.Id);
            Assert.Equal(1, await client.GetOffsetAsync(default));
            await using var enumerator = client.Topic("events").SubscribeAsync().GetAsyncEnumerator();
            Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal("accepted", DecodeString(enumerator.Current));

            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Subscription_FollowsContinueAsNewWithStateAndWaitingPoll()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<ContinuingStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (ContinuingStreamWorkflow workflow) => workflow.RunAsync(null, true),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var initialRunId = handle.FirstExecutionRunId;
            await using var client = new WorkflowStreamClient(
                Client,
                handle.Id,
                new() { BatchInterval = TimeSpan.FromHours(1) });
            await using var enumerator = client.SubscribeAsync().GetAsyncEnumerator();

            client.Topic("events").Publish("before");
            await client.FlushAsync(default);
            Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal("before", DecodeString(enumerator.Current));

            var waitingMove = enumerator.MoveNextAsync().AsTask();
            await handle.SignalAsync(workflow => workflow.ContinueAsync());
            await AssertMore.EventuallyAsync(async () =>
            {
                var description = await handle.DescribeAsync();
                Assert.NotEqual(initialRunId, description.RunId);
            });

            client.Topic("events").Publish("after");
            await client.FlushAsync(default);
            Assert.True(await waitingMove.WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal("after", DecodeString(enumerator.Current));
            Assert.Equal(1, enumerator.Current.Offset);

            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();
        });
    }

    private static string DecodeString(WorkflowStreamItem item) =>
        (string)DataConverter.Default.PayloadConverter.ToValue(item.Payload, typeof(string))!;

    [Workflow]
    public class HostedStreamWorkflow
    {
        private readonly WorkflowStream stream = new();
        private bool finished;

        [WorkflowRun]
        public async Task RunAsync()
        {
            await Workflow.WaitConditionAsync(() => finished);
        }

        [WorkflowSignal]
        public Task FinishAsync()
        {
            finished = true;
            return Task.CompletedTask;
        }

        [WorkflowSignal]
        public Task TruncateAsync(long offset)
        {
            stream.Truncate(offset);
            return Task.CompletedTask;
        }

        [WorkflowQuery]
        public long BaseOffset() => stream.GetState().BaseOffset;
    }

    [Workflow]
    public class ContinuingStreamWorkflow
    {
        private readonly WorkflowStream stream;
        private bool continueRequested;
        private bool finished;

        [WorkflowInit]
        public ContinuingStreamWorkflow(WorkflowStreamState? state, bool continueOnce)
        {
            stream = new(state);
        }

        [WorkflowRun]
        public async Task RunAsync(WorkflowStreamState? state, bool continueOnce)
        {
            if (continueOnce)
            {
                await Workflow.WaitConditionAsync(() => continueRequested);
                await stream.ContinueAsNewAsync(nextState =>
                    Workflow.CreateContinueAsNewException(
                        (ContinuingStreamWorkflow workflow) =>
                            workflow.RunAsync(nextState, false)));
            }
            await Workflow.WaitConditionAsync(() => finished);
        }

        [WorkflowSignal]
        public Task ContinueAsync()
        {
            continueRequested = true;
            return Task.CompletedTask;
        }

        [WorkflowSignal]
        public Task FinishAsync()
        {
            finished = true;
            return Task.CompletedTask;
        }
    }
}
