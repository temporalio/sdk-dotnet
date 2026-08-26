namespace Temporalio.Tests.Extensions.WorkflowStreams;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Temporalio.Client;
using Temporalio.Extensions.WorkflowStreams;
using Temporalio.Extensions.WorkflowStreams.Internal;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

public class ListenerSubscribeTests : WorkflowEnvironmentTestBase
{
    private static readonly TimeSpan BatchInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PollCooldown = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public ListenerSubscribeTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task Listener_DeliversItemsAndAdvancesOffset()
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

                var listener = new RecordingListener();
                using (var subscriptionHandle = streamClient.Subscribe(FastPoll(), listener))
                {
                    await AwaitItemsAsync(listener, 2);
                    var first = listener.Items[0];
                    Assert.Equal("evt", first.Topic);
                    Assert.Equal("a", WorkflowStreamTestUtils.Decode(first));
                    Assert.Equal(0, first.Offset);

                    var second = listener.Items[1];
                    Assert.Equal("b", WorkflowStreamTestUtils.Decode(second));
                    Assert.Equal(1, second.Offset);
                }

                Assert.Equal(2, await streamClient.GetOffsetAsync());
                Assert.Null(listener.Error);
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task TopicHandleListenerSubscribe_Filters()
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

                var listener = new RecordingListener();
                using (var subscriptionHandle = streamClient.Topic("a").Subscribe(0, listener))
                {
                    await AwaitItemsAsync(listener, 2);
                    Assert.Equal("1", WorkflowStreamTestUtils.Decode(listener.Items[0]));
                    var second = listener.Items[1];
                    Assert.Equal("3", WorkflowStreamTestUtils.Decode(second));
                    Assert.Equal(2, second.Offset);
                }
                Assert.Null(listener.Error);
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Terminal_CallsOnCompleted()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();

                var listener = new RecordingListener();
                var subscriptionHandle = streamClient.Subscribe(FastPoll(), listener);
                await AwaitItemsAsync(listener, 1);

                // Complete the workflow, then keep polling: the subscription must end cleanly
                // with OnCompleted rather than surface an error.
                await handle.SignalAsync("Finish", Array.Empty<object?>());
                await handle.GetResultAsync();

                await listener.CompletedTask.WaitAsync(Timeout);
                Assert.Null(listener.Error);
                await subscriptionHandle.Completion.WaitAsync(Timeout);
            }
        });
    }

    [Fact]
    public async Task Listener_FollowsContinueAsNew()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();

                var listener = new RecordingListener();
                using (var subscriptionHandle = streamClient.Subscribe(FastPoll(), listener))
                {
                    await AwaitItemsAsync(listener, 1);
                    Assert.Equal("a", WorkflowStreamTestUtils.Decode(listener.Items[0]));

                    // Roll the workflow over to a new run. The stream state (including item "a")
                    // is carried across the continue-as-new boundary.
                    await handle.SignalAsync("Rollover", Array.Empty<object?>());
                    streamClient.Topic("evt").Publish("b", forceFlush: true);
                    await streamClient.FlushAsync();

                    // The subscription retries through the rollover and picks up on the
                    // successor run where the prior log — and so the subscriber's offset — is
                    // preserved.
                    await AwaitItemsAsync(listener, 2);
                    var second = listener.Items[1];
                    Assert.Equal("b", WorkflowStreamTestUtils.Decode(second));
                    Assert.Equal(1, second.Offset);
                    Assert.Null(listener.Error);
                }
            }
            await Client.GetWorkflowHandle(handle.Id).SignalAsync("Finish", Array.Empty<object?>());
            await Client.GetWorkflowHandle(handle.Id).GetResultAsync();
        });
    }

    [Fact]
    public async Task PollRpcDeadline_RetriesAcceptedUpdate()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            var listener = new RecordingListener();
            var driver = new SubscriptionDriver(
                Client,
                handle.Id,
                FastPoll(),
                listener,
                _ => { },
                TimeSpan.FromMilliseconds(10));
            driver.Start();

            await handle.SignalAsync(
                "PublishLocalAfterDelay",
                new object?[] { "evt", "after-deadline", TimeSpan.FromMilliseconds(250) });
            await AwaitItemsAsync(listener, 1);

            Assert.Equal("after-deadline", WorkflowStreamTestUtils.Decode(listener.Items[0]));
            Assert.Null(listener.Error);
            driver.Close();
            await driver.Completion.WaitAsync(Timeout);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Listener_TruncationResetsOffset()
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
                await AssertMore.EqualEventuallyAsync(3, () => streamClient.GetOffsetAsync());

                await handle.ExecuteUpdateAsync("Truncate", new object?[] { 2L });

                // A subscription positioned before the new base offset restarts from the
                // beginning of whatever still exists instead of failing.
                var listener = new RecordingListener();
                using (var subscriptionHandle = streamClient.Subscribe(
                    new SubscribeOptions { FromOffset = 1, PollCooldown = PollCooldown }, listener))
                {
                    await AwaitItemsAsync(listener, 1);
                    var item = listener.Items[0];
                    Assert.Equal("c", WorkflowStreamTestUtils.Decode(item));
                    Assert.Equal(2, item.Offset);
                    Assert.Null(listener.Error);
                }
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task Dispose_StopsDeliveryWithoutOnCompleted()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();

                var listener = new RecordingListener();
                var subscriptionHandle = streamClient.Subscribe(FastPoll(), listener);
                await AwaitItemsAsync(listener, 1);

                subscriptionHandle.Dispose();
                await subscriptionHandle.Completion.WaitAsync(Timeout);

                streamClient.Topic("evt").Publish("b", forceFlush: true);
                await streamClient.FlushAsync();
                await AssertMore.EqualEventuallyAsync(2, () => streamClient.GetOffsetAsync());
                Assert.Single(listener.Items);
                Assert.False(listener.CompletedTask.IsCompleted, "user-initiated dispose must not call OnCompleted");
                Assert.Null(listener.Error);
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task PendingOnNextAsyncTask_DefersDelivery()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                var listener = new RecordingListener();
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                listener.Gates.Enqueue(gate.Task);

                streamClient.Topic("evt").Publish("a");
                streamClient.Topic("evt").Publish("b");
                await streamClient.FlushAsync();

                using (var subscriptionHandle = streamClient.Subscribe(FastPoll(), listener))
                {
                    await AwaitItemsAsync(listener, 1);
                    // The first item's task is pending, so the second must not be delivered yet.
                    Assert.Single(listener.Items);

                    gate.SetResult();
                    await AwaitItemsAsync(listener, 2);
                    Assert.Equal("b", WorkflowStreamTestUtils.Decode(listener.Items[1]));
                    Assert.Null(listener.Error);
                }
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task OnNextAsyncThrowing_StopsSubscription()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                var boom = new InvalidOperationException("boom");
                var listener = new RecordingListener { OnNextFailure = boom };

                streamClient.Topic("evt").Publish("a");
                streamClient.Topic("evt").Publish("b");
                await streamClient.FlushAsync();

                var subscriptionHandle = streamClient.Subscribe(FastPoll(), listener);
                var error = await listener.ErrorTask.WaitAsync(Timeout);
                Assert.Same(boom, error);
                var completionFailure = await Assert.ThrowsAnyAsync<Exception>(
                    () => subscriptionHandle.Completion.WaitAsync(Timeout));
                Assert.Same(boom, completionFailure);
                Assert.Single(listener.Items);
                Assert.False(listener.CompletedTask.IsCompleted);
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task FaultedOnNextAsyncTask_StopsSubscription()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                var boom = new InvalidOperationException("stage failed");
                var listener = new RecordingListener();
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                listener.Gates.Enqueue(gate.Task);

                streamClient.Topic("evt").Publish("a");
                streamClient.Topic("evt").Publish("b");
                await streamClient.FlushAsync();

                var subscriptionHandle = streamClient.Subscribe(FastPoll(), listener);
                await AwaitItemsAsync(listener, 1);
                gate.SetException(boom);

                var error = await listener.ErrorTask.WaitAsync(Timeout);
                Assert.Same(boom, error);
                var completionFailure = await Assert.ThrowsAnyAsync<Exception>(
                    () => subscriptionHandle.Completion.WaitAsync(Timeout));
                Assert.Same(boom, completionFailure);
                Assert.Single(listener.Items);
            }
            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task ManySubscriptions_AllDeliverAndComplete()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            using (var streamClient = NewStreamClient(handle.Id))
            {
                // 6 concurrent subscriptions on one client all make progress without any
                // thread being held while a poll is blocked on the server.
                var listeners = new List<RecordingListener>();
                for (var i = 0; i < 6; i++)
                {
                    var listener = new RecordingListener();
                    listeners.Add(listener);
                    streamClient.Subscribe(FastPoll(), listener);
                }

                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();
                foreach (var listener in listeners)
                {
                    await AwaitItemsAsync(listener, 1);
                }

                await handle.SignalAsync("Finish", Array.Empty<object?>());
                await handle.GetResultAsync();
                foreach (var listener in listeners)
                {
                    await listener.CompletedTask.WaitAsync(Timeout);
                    Assert.Null(listener.Error);
                }
            }
        });
    }

    [Fact]
    public async Task ClientCloseAsync_StopsSubscriptions()
    {
        await ExecuteWorkerAsync<StreamHostWorkflow>(async worker =>
        {
            var handle = await StartHostWorkflowAsync(worker);
            var listener = new RecordingListener();
            WorkflowStreamSubscriptionHandle subscriptionHandle;
            using (var streamClient = NewStreamClient(handle.Id))
            {
                streamClient.Topic("evt").Publish("a", forceFlush: true);
                await streamClient.FlushAsync();
                subscriptionHandle = streamClient.Subscribe(FastPoll(), listener);
                await AwaitItemsAsync(listener, 1);
                await streamClient.CloseAsync();
            }
            // Closing the client stops the subscription cleanly.
            await subscriptionHandle.Completion.WaitAsync(Timeout);
            Assert.Null(listener.Error);
            Assert.False(listener.CompletedTask.IsCompleted);

            await handle.SignalAsync("Finish", Array.Empty<object?>());
            await handle.GetResultAsync();
        });
    }

    private static SubscribeOptions FastPoll() => new() { PollCooldown = PollCooldown };

    private static Task AwaitItemsAsync(RecordingListener listener, int count) =>
        AssertMore.EventuallyAsync(
            () =>
            {
                Assert.True(
                    listener.Items.Count >= count,
                    $"timed out waiting for {count} items, got {listener.Items.Count}");
                return Task.CompletedTask;
            },
            interval: TimeSpan.FromMilliseconds(100),
            iterations: 100);

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
        using var worker = new TemporalWorker(Client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    // Records callbacks. Gates feeds OnNextAsync return tasks in delivery order; once drained,
    // OnNextAsync returns a completed task (proceed immediately).
    private sealed class RecordingListener : WorkflowStreamListener
    {
        private readonly object lockObj = new();
        private readonly List<WorkflowStreamItem> items = new();
        private readonly TaskCompletionSource completed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<Exception> failed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Queue<Task> Gates { get; } = new();

        public Exception? OnNextFailure { get; set; }

        public Exception? Error { get; private set; }

        public Task CompletedTask => completed.Task;

        public Task<Exception> ErrorTask => failed.Task;

        public List<WorkflowStreamItem> Items
        {
            get
            {
                lock (lockObj)
                {
                    return new List<WorkflowStreamItem>(items);
                }
            }
        }

        public override Task OnNextAsync(WorkflowStreamItem item)
        {
            Task? gate = null;
            lock (lockObj)
            {
                items.Add(item);
                if (Gates.Count > 0)
                {
                    gate = Gates.Dequeue();
                }
            }
            if (OnNextFailure != null)
            {
                throw OnNextFailure;
            }
            return gate ?? Task.CompletedTask;
        }

        public override void OnError(Exception failure)
        {
            Error = failure;
            failed.TrySetResult(failure);
        }

        public override void OnCompleted() => completed.TrySetResult();
    }
}
