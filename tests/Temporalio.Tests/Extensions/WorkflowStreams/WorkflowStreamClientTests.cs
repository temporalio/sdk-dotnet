namespace Temporalio.Tests.Extensions.WorkflowStreams;

using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowStreamClientTests : WorkflowEnvironmentTestBase
{
    public WorkflowStreamClientTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public void Options_AreValidatedAndCloneIndependently()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowStreamClient(
            Client,
            "workflow-id",
            new() { BatchInterval = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowStreamClient(
            Client,
            "workflow-id",
            new() { MaxBatchSize = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowStreamClient(
            Client,
            "workflow-id",
            new() { MaxRetryDuration = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowStreamClient(
            Client,
            "workflow-id",
            new() { RpcTimeout = TimeSpan.Zero }));

        var options = new WorkflowStreamClientOptions { MaxBatchSize = 1 };
        var clone = Assert.IsType<WorkflowStreamClientOptions>(options.Clone());
        options.MaxBatchSize = -1;
        Assert.Equal(1, clone.MaxBatchSize);
    }

    [Fact]
    public async Task GetTopic_AllowsTypedAndUntypedHandlesWithSameName()
    {
        await using var client = new WorkflowStreamClient(Client, "workflow-id");

        Assert.NotSame(client.GetTopic("topic"), client.GetTopic("topic"));
        Assert.Equal("topic", client.GetTopic<string>("topic").Name);
        Assert.Equal("topic", client.GetTopic("topic").Name);
    }

    [Fact]
    public async Task WorkflowGetTopic_AllowsTypedAndUntypedHandlesWithSameName()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<TopicHandlesWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (TopicHandlesWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.True(await handle.GetResultAsync());
        });
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
            var orders = client.GetTopic("orders").SubscribeAsync();

            client.GetTopic("orders").Publish("first");
            client.GetTopic("audit").Publish("ignored");
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
    public async Task SubscribeAfterCompletion_DrainsFilteredPagesWithIndependentEnumerations()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            {
                MaxCachedWorkflows = 0,
            }.AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var value = new string('x', 400_000);
            await handle.SignalAsync(workflow => workflow.PublishAsync("events", value));
            await handle.SignalAsync(workflow => workflow.PublishAsync("other", "ignored"));
            await handle.SignalAsync(workflow => workflow.PublishAsync("events", value));
            await handle.SignalAsync(workflow => workflow.PublishAsync("events", value));
            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();

            await using var client = new WorkflowStreamClient(Client, handle.Id);
            var historyCount = (await handle.FetchHistoryAsync()).Events.Count;
            var subscription = client.GetTopic<string>("events").SubscribeAsync();
            for (var iteration = 0; iteration < 2; iteration++)
            {
                var offsets = new List<long>();
                await foreach (var item in subscription)
                {
                    Assert.Equal(value, item.Value);
                    offsets.Add(item.Offset);
                }
                Assert.Equal(new long[] { 0, 2, 3 }, offsets);
            }
            Assert.Equal(historyCount, (await handle.FetchHistoryAsync()).Events.Count);
        });
    }

    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Canceled)]
    [InlineData(WorkflowExecutionStatus.Terminated)]
    [InlineData(WorkflowExecutionStatus.TimedOut)]
    public async Task SubscribeAfterClosure_DrainsEveryTerminalStatus(WorkflowExecutionStatus status)
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
            {
                MaxCachedWorkflows = 0,
            }.AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    RunTimeout = status == WorkflowExecutionStatus.TimedOut ?
                        TimeSpan.FromSeconds(5) : null,
                });
            await handle.SignalAsync(workflow => workflow.PublishAsync("events", "retained"));
            await using var client = new WorkflowStreamClient(Client, handle.Id);
            Assert.Equal(1, await client.GetOffsetAsync());
            if (status == WorkflowExecutionStatus.Completed)
            {
                await handle.SignalAsync(workflow => workflow.FinishAsync());
                await handle.GetResultAsync();
            }
            else
            {
                if (status == WorkflowExecutionStatus.Failed)
                {
                    await handle.SignalAsync(workflow => workflow.FailAsync());
                }
                else if (status == WorkflowExecutionStatus.Canceled)
                {
                    await handle.CancelAsync();
                }
                else if (status == WorkflowExecutionStatus.Terminated)
                {
                    await handle.TerminateAsync();
                }
                await Assert.ThrowsAsync<WorkflowFailedException>(() => handle.GetResultAsync());
            }
            Assert.Equal(status, (await handle.DescribeAsync()).Status);
            await using var enumerator = client.GetTopic<string>("events").SubscribeAsync().
                GetAsyncEnumerator();
            Assert.True(await enumerator.MoveNextAsync());
            Assert.Equal("retained", enumerator.Current.Value);
            Assert.False(await enumerator.MoveNextAsync());
        });
    }

    [Fact]
    public async Task ClosedStreamPagination_StaysPinnedAfterWorkflowIdReuse()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var value = new string('x', 400_000);
            for (var index = 0; index < 3; index++)
            {
                await handle.SignalAsync(workflow => workflow.PublishAsync("events", value));
            }
            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();
            await using var client = new WorkflowStreamClient(Client, handle.Id);
            await using var enumerator = client.GetTopic<string>("events").SubscribeAsync().
                GetAsyncEnumerator();
            Assert.True(await enumerator.MoveNextAsync());
            Assert.Equal(value, enumerator.Current.Value);
            var replacement = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: handle.Id, taskQueue: worker.Options.TaskQueue!));
            await replacement.SignalAsync(workflow => workflow.PublishAsync("events", "replacement"));
            for (var index = 1; index < 3; index++)
            {
                Assert.True(await enumerator.MoveNextAsync());
                Assert.Equal(value, enumerator.Current.Value);
                Assert.Equal(index, enumerator.Current.Offset);
            }
            Assert.False(await enumerator.MoveNextAsync());
            await replacement.SignalAsync(workflow => workflow.FinishAsync());
            await replacement.GetResultAsync();
        });
    }

    [Fact]
    public async Task SubscribeAfterCompletion_ClampsTruncationAndCompletesEmptyReads()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await handle.SignalAsync(workflow => workflow.PublishAsync("events", "zero"));
            await handle.SignalAsync(workflow => workflow.PublishAsync("events", "one"));
            await handle.SignalAsync(workflow => workflow.PublishAsync("events", "two"));
            await handle.SignalAsync(workflow => workflow.TruncateAsync(2));
            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();
            await using var client = new WorkflowStreamClient(Client, handle.Id);
            await using var retained = client.SubscribeAsync<string>(new() { FromOffset = 1 }).
                GetAsyncEnumerator();
            Assert.True(await retained.MoveNextAsync());
            Assert.Equal("two", retained.Current.Value);
            Assert.Equal(2, retained.Current.Offset);
            Assert.False(await retained.MoveNextAsync());

            await using var exhausted = client.SubscribeAsync(new() { FromOffset = 3 }).
                GetAsyncEnumerator();
            Assert.False(await exhausted.MoveNextAsync());
            await using var missing = client.GetTopic("missing").SubscribeAsync().GetAsyncEnumerator();
            Assert.False(await missing.MoveNextAsync());
        });
    }

    [Fact]
    public async Task SubscribeAfterCompletion_PropagatesConfiguredQueryRejection()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await handle.SignalAsync(workflow => workflow.FinishAsync());
            await handle.GetResultAsync();
            var options = (TemporalClientOptions)Client.Options.Clone();
            options.QueryRejectCondition = QueryRejectCondition.NotOpen;
            var rejectingClient = new TemporalClient(Client.Connection, options);
            await using var client = new WorkflowStreamClient(rejectingClient, handle.Id);
            await using var enumerator = client.SubscribeAsync().GetAsyncEnumerator();
            await Assert.ThrowsAsync<WorkflowQueryRejectedException>(
                () => enumerator.MoveNextAsync().AsTask());
        });
    }

    [Fact]
    public async Task SubscribeAfterCompletion_PropagatesMissingPollQuery()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<NoStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (NoStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await handle.GetResultAsync();
            await using var client = new WorkflowStreamClient(Client, handle.Id);
            await using var enumerator = client.SubscribeAsync().GetAsyncEnumerator();
            await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => enumerator.MoveNextAsync().AsTask());
        });
    }

    [Fact]
    public async Task PollQuery_ReturnsImmediatelyWhileRunningAndDraining()
    {
        using var worker = new TemporalWorker(
            Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}").AddWorkflow<HostedStreamWorkflow>());
        await worker.ExecuteAsync(async () =>
        {
            var handle = await Client.StartWorkflowAsync(
                (HostedStreamWorkflow workflow) => workflow.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var empty = await handle.QueryAsync<PollResult>(
                WorkflowStreamConstants.PollQueryName, new object?[] { new PollInput() });
            Assert.Empty(empty.Items);
            Assert.False(empty.MoreReady);
            await handle.SignalAsync(workflow => workflow.DetachAsync());
            var draining = await handle.QueryAsync<PollResult>(
                WorkflowStreamConstants.PollQueryName, new object?[] { new PollInput() });
            Assert.Empty(draining.Items);
            await handle.SignalAsync(workflow => workflow.FinishAsync());
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
            client.GetTopic(string.Empty).Publish("zero");
            client.GetTopic(string.Empty).Publish("one");
            client.GetTopic(string.Empty).Publish("two");
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
            await using var enumerator = client.GetTopic("events").SubscribeAsync().GetAsyncEnumerator();
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

            client.GetTopic("events").Publish("before");
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

            client.GetTopic("events").Publish("after");
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
    public class NoStreamWorkflow
    {
        [WorkflowRun]
        public Task RunAsync() => Task.CompletedTask;
    }

    [Workflow]
    public class HostedStreamWorkflow
    {
        private readonly WorkflowStream stream = new();
        private bool finished;
        private bool fail;

        [WorkflowRun]
        public async Task RunAsync()
        {
            await Workflow.WaitConditionAsync(() => finished);
            if (fail)
            {
                throw new ApplicationFailureException("failed after persisting stream state");
            }
        }

        [WorkflowSignal]
        public Task FailAsync()
        {
            fail = true;
            finished = true;
            return Task.CompletedTask;
        }

        [WorkflowSignal]
        public Task FinishAsync()
        {
            finished = true;
            return Task.CompletedTask;
        }

        [WorkflowSignal]
        public Task PublishAsync(string topic, string value)
        {
            stream.GetTopic(topic).Publish(value);
            return Task.CompletedTask;
        }

        [WorkflowSignal]
        public Task DetachAsync()
        {
            stream.DetachPollers();
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
                var nextState = await stream.CaptureStateForContinueAsNewAsync();
                throw Workflow.CreateContinueAsNewException(
                    (ContinuingStreamWorkflow workflow) =>
                        workflow.RunAsync(nextState, false));
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

    [Workflow]
    public class TopicHandlesWorkflow
    {
        [WorkflowRun]
        public Task<bool> RunAsync()
        {
            var stream = new WorkflowStream();
            var firstUntyped = stream.GetTopic("topic");
            var typed = stream.GetTopic<string>("topic");
            var secondUntyped = stream.GetTopic("topic");
            return Task.FromResult(
                firstUntyped.Name == typed.Name &&
                typed.Name == secondUntyped.Name &&
                !ReferenceEquals(firstUntyped, secondUntyped));
        }
    }
}
