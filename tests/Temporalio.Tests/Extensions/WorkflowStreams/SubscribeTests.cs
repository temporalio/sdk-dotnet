namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System;
using System.Threading;
using System.Threading.Tasks;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

// Integration tests for the pull-based subscription API, ported from the Java module's
// SubscribeTest.
public class SubscribeTests : WorkflowEnvironmentTestBase
{
    private static readonly TimeSpan BatchInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PollCooldown = TimeSpan.FromMilliseconds(50);

    public SubscribeTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public void Client_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new WorkflowStreamClient(null!, "workflow-id"));
        Assert.Throws<ArgumentNullException>(() => new WorkflowStreamClient(Client, null!));
        using var streamClient = new WorkflowStreamClient(Client, "workflow-id");
        Assert.Throws<ArgumentNullException>(() => streamClient.Topic(null!));
    }

    [Fact]
    public async Task Subscribe_DeliversItemsAndAdvancesOffset()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();
                // A workflow-side publish lands on the same log.
                await handle.SignalAsync("PublishLocal", new object?[] { "evt", "b" });

                using (var subscription = streamClient.Subscribe(FastPoll()))
                {
                    Assert.True(await subscription.MoveNextAsync());
                    var first = subscription.Current;
                    Assert.Equal("evt", first.Topic);
                    Assert.Equal("a", WorkflowStreamTestUtils.Decode(first));
                    Assert.Equal(0, first.Offset);

                    Assert.True(await subscription.MoveNextAsync());
                    var second = subscription.Current;
                    Assert.Equal("b", WorkflowStreamTestUtils.Decode(second));
                    Assert.Equal(1, second.Offset);
                }

                Assert.Equal(2, await streamClient.GetOffsetAsync());
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task TopicHandleSubscribe_Filters()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("a").Publish("1");
                streamClient.Topic("b").Publish("2");
                streamClient.Topic("a").Publish("3");
                await streamClient.FlushAsync();

                using var subscription = streamClient.Topic("a").Subscribe(0);
                var seen = 0;
                // Exercises the IAsyncEnumerable path.
                await foreach (var item in subscription)
                {
                    seen++;
                    if (seen == 1)
                    {
                        Assert.Equal("1", WorkflowStreamTestUtils.Decode(item));
                        Assert.Equal(0, item.Offset);
                    }
                    else
                    {
                        Assert.Equal("3", WorkflowStreamTestUtils.Decode(item));
                        Assert.Equal(2, item.Offset);
                        break;
                    }
                }
                Assert.Equal(2, seen);
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task TopicHandleSubscribe_EmptyTopic()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic(string.Empty).Publish("no-topic");
                await streamClient.FlushAsync();

                using var subscription = streamClient.Topic(string.Empty).Subscribe();
                Assert.True(await subscription.MoveNextAsync());
                Assert.Equal(string.Empty, subscription.Current.Topic);
                Assert.Equal("no-topic", WorkflowStreamTestUtils.Decode(subscription.Current));
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Subscribe_EndsCleanlyOnTerminal()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();

                using var subscription = streamClient.Subscribe(FastPoll());
                Assert.True(await subscription.MoveNextAsync());
                Assert.Equal("a", WorkflowStreamTestUtils.Decode(subscription.Current));

                // Complete the workflow, then keep polling: the subscription must end cleanly
                // rather than surface an error.
                await handle.SignalAsync("Finish", Array.Empty<object?>());
                await handle.GetResultAsync();
                Assert.False(
                    await subscription.MoveNextAsync(),
                    "terminal workflow should end the stream without surfacing an error");
            }
        });
    }

    [Fact]
    public async Task Subscribe_FollowsContinueAsNew()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();

                using var subscription = streamClient.Subscribe(FastPoll());
                Assert.True(await subscription.MoveNextAsync());
                Assert.Equal("a", WorkflowStreamTestUtils.Decode(subscription.Current));
                Assert.Equal(0, subscription.Current.Offset);

                // Roll the workflow over to a new run. The stream state (including item "a")
                // is carried across the continue-as-new boundary.
                await handle.SignalAsync("Rollover", Array.Empty<object?>());
                streamClient.Topic("evt").Publish("b", forceFlush: true);
                await streamClient.FlushAsync();

                // The subscription retries through the rollover (draining rejections, polls
                // lost to the closing run) and picks up on the successor run where the prior
                // log — and so the subscriber's offset — is preserved.
                Assert.True(await subscription.MoveNextAsync());
                Assert.Equal("b", WorkflowStreamTestUtils.Decode(subscription.Current));
                Assert.Equal(1, subscription.Current.Offset);
            }

            // The workflow now runs its successor; finish whatever run is current.
            await Client.GetWorkflowHandle(handle.Id).SignalAsync("Finish", Array.Empty<object?>());
            await Client.GetWorkflowHandle(handle.Id).GetResultAsync();
        });
    }

    [Fact]
    public async Task Subscribe_TruncationResetsOffset()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a");
                streamClient.Topic("evt").Publish("b");
                streamClient.Topic("evt").Publish("c");
                await streamClient.FlushAsync();
                // Confirm the batch has been applied before truncating.
                using (var warmup = streamClient.Subscribe(FastPoll()))
                {
                    Assert.True(await warmup.MoveNextAsync());
                }

                await handle.ExecuteUpdateAsync("Truncate", new object?[] { 2L });

                // A subscription positioned before the new base offset restarts from the
                // beginning of whatever still exists instead of failing.
                using var subscription = streamClient.Subscribe(
                    new SubscribeOptions { FromOffset = 1, PollCooldown = PollCooldown });
                Assert.True(await subscription.MoveNextAsync());
                Assert.Equal("c", WorkflowStreamTestUtils.Decode(subscription.Current));
                Assert.Equal(2, subscription.Current.Offset);

                Assert.Equal(3, await streamClient.GetOffsetAsync());
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task UnrecoverableError_SurfacesFromMoveNextAsync()
    {
        using (var streamClient = new WorkflowStreamClient(Client, $"workflow-that-does-not-exist-{Guid.NewGuid()}"))
        {
            using var subscription = streamClient.Subscribe(FastPoll());
            // The workflow does not exist, which is neither a rollover nor a terminal end, so
            // the failure surfaces to the consumer.
            var exception = await Assert.ThrowsAsync<RpcException>(() => subscription.MoveNextAsync());
            Assert.Equal(RpcException.StatusCode.NotFound, exception.Code);
            Assert.False(
                await subscription.MoveNextAsync(),
                "the subscription is over after an unrecoverable error");
        }
    }

    [Fact]
    public async Task Dispose_StopsIteration()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                var subscription = streamClient.Subscribe(FastPoll());
#pragma warning disable CA1849, VSTHRD103 // The test exercises the synchronous Dispose path
                subscription.Dispose();
#pragma warning restore CA1849, VSTHRD103
                Assert.False(await subscription.MoveNextAsync());
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task AsyncEnumerationCancellation_InterruptsWaitingPoll()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            using (var cancellation = new CancellationTokenSource())
            await using (var subscription = streamClient.Subscribe(FastPoll()))
            {
                var enumerator = subscription.GetAsyncEnumerator(cancellation.Token);
                var moveNext = enumerator.MoveNextAsync().AsTask();
                await cancellation.CancelAsync();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => moveNext.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Publish_FromActivity()
    {
        await ExecuteWorkerAsync<ActivityPublishWorkflow>(async worker =>
        {
            var handle = await Client.StartWorkflowAsync(
                (ActivityPublishWorkflow wf) => wf.RunAsync(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

            using (var streamClient = NewStreamClient(handle.Id))
            {
                using var subscription = streamClient.Subscribe(FastPoll());
                Assert.True(await subscription.MoveNextAsync());
                Assert.Equal("from-activity", WorkflowStreamTestUtils.Decode(subscription.Current));
                Assert.Equal(0, subscription.Current.Offset);
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    private static SubscribeOptions FastPoll() => new() { PollCooldown = PollCooldown };

    private WorkflowStreamClient NewStreamClient(string workflowId) =>
        new(Client, workflowId, new WorkflowStreamClientOptions { BatchInterval = BatchInterval });

    private async Task<WorkflowHandle<StreamHostWorkflow>> StartHostWorkflowAsync(TemporalWorker worker)
    {
        var handle = await Client.StartWorkflowAsync(
            (StreamHostWorkflow wf) => wf.RunAsync(null),
            new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
        await WorkflowStreamTestUtils.WaitStreamReadyAsync(handle);
        return handle;
    }

    private async Task ExecuteWorkerAsync<TWorkflow>(
        Func<TemporalWorker, Task> action, TemporalWorkerOptions? options = null)
    {
        options = (TemporalWorkerOptions?)options?.Clone() ?? new();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        if (typeof(TWorkflow) == typeof(ActivityPublishWorkflow))
        {
            options.AddActivity(ActivityPublishActivities.PublishFromActivityAsync);
        }
        using var worker = new TemporalWorker(Client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    // Schedules the stream-publishing activity, then waits to be finished so the subscription
    // deterministically observes the item while the workflow is still running.
    [Workflow]
    public class ActivityPublishWorkflow
    {
        private readonly WorkflowStream stream;
        private bool finished;

        [WorkflowInit]
        public ActivityPublishWorkflow() => stream = new();

        [WorkflowRun]
        public async Task RunAsync()
        {
            await Workflow.ExecuteActivityAsync(
                () => ActivityPublishActivities.PublishFromActivityAsync(),
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(2) });
            await Workflow.WaitConditionAsync(() => finished);
        }

        [WorkflowSignal]
        public Task FinishAsync()
        {
            finished = true;
            return Task.CompletedTask;
        }
    }

    public static class ActivityPublishActivities
    {
        [Activity]
        public static async Task PublishFromActivityAsync()
        {
            using var client = WorkflowStreamClient.FromActivity(
                new WorkflowStreamClientOptions { BatchInterval = BatchInterval });
            client.Topic("evt").Publish("from-activity", forceFlush: true);
            await client.CloseAsync();
        }
    }
}
