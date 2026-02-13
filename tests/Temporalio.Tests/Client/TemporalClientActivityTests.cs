#pragma warning disable xUnit1013 // We want public static methods as activities
namespace Temporalio.Tests.Client;

using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

public class TemporalClientActivityTests : WorkflowEnvironmentTestBase
{
    private static volatile TaskCompletionSource? waitForCancelReached;

    public TemporalClientActivityTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Activity]
    public static Task<string> SimpleActivityAsync(string input) =>
        Task.FromResult($"echo:{input}");

    [Activity]
    public static Task VoidActivityAsync() => Task.CompletedTask;

    [Activity]
    public static async Task WaitForCancelAsync()
    {
        var ctx = ActivityExecutionContext.Current;
        waitForCancelReached?.TrySetResult();
        while (!ctx.CancellationToken.IsCancellationRequested)
        {
            ctx.Heartbeat();
            await Task.Delay(100, ctx.CancellationToken);
        }
        ctx.CancellationToken.ThrowIfCancellationRequested();
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleWithResult_Succeeds()
    {
        await ExecuteActivityWorkerAsync(SimpleActivityAsync, async taskQueue =>
        {
            var result = await Client.ExecuteActivityAsync(
                () => SimpleActivityAsync("hello"),
                new($"act-{Guid.NewGuid()}", taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            Assert.Equal("echo:hello", result);
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_VoidResult_Succeeds()
    {
        await ExecuteActivityWorkerAsync(VoidActivityAsync, async taskQueue =>
        {
            await Client.ExecuteActivityAsync(
                () => VoidActivityAsync(),
                new($"act-{Guid.NewGuid()}", taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_ByName_Succeeds()
    {
        await ExecuteActivityWorkerAsync(SimpleActivityAsync, async taskQueue =>
        {
            var result = await Client.ExecuteActivityAsync<string>(
                "SimpleActivity",
                new object?[] { "world" },
                new($"act-{Guid.NewGuid()}", taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            Assert.Equal("echo:world", result);
        });
    }

    [Fact]
    public async Task StartActivityAsync_AlreadyStarted_Throws()
    {
        await ExecuteActivityWorkerAsync(WaitForCancelAsync, async taskQueue =>
        {
            var activityId = $"act-{Guid.NewGuid()}";
            var handle = await Client.StartActivityAsync(
                () => WaitForCancelAsync(),
                new(activityId, taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                    IdConflictPolicy = ActivityIdConflictPolicy.Fail,
                });

            // Try to start again with same ID
            var err = await Assert.ThrowsAsync<ActivityAlreadyStartedException>(() =>
                Client.StartActivityAsync(
                    () => WaitForCancelAsync(),
                    new(activityId, taskQueue)
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                        IdConflictPolicy = ActivityIdConflictPolicy.Fail,
                    }));
            Assert.Equal(activityId, err.ActivityId);
            Assert.Equal("WaitForCancel", err.ActivityType);
            Assert.NotNull(err.RunId);

            // Cleanup
            await handle.TerminateAsync();
        });
    }

    [Fact]
    public async Task StartActivityAsync_IdReusePolicyRejectDuplicate_Throws()
    {
        await ExecuteActivityWorkerAsync(SimpleActivityAsync, async taskQueue =>
        {
            var activityId = $"act-{Guid.NewGuid()}";
            var opts = new StartActivityOptions(activityId, taskQueue)
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                IdReusePolicy = ActivityIdReusePolicy.RejectDuplicate,
            };

            // Start and complete first activity
            var handle = await Client.StartActivityAsync(
                () => SimpleActivityAsync("first"), opts);
            await handle.GetResultAsync();

            // Try to start again with same ID - should fail
            var err = await Assert.ThrowsAsync<ActivityAlreadyStartedException>(() =>
                Client.StartActivityAsync(
                    () => SimpleActivityAsync("second"), opts));
            Assert.Equal(activityId, err.ActivityId);
        });
    }

    [Fact]
    public async Task GetActivityHandle_ExistingActivity_Succeeds()
    {
        await ExecuteActivityWorkerAsync(SimpleActivityAsync, async taskQueue =>
        {
            var activityId = $"act-{Guid.NewGuid()}";
            var handle = await Client.StartActivityAsync(
                () => SimpleActivityAsync("test"),
                new(activityId, taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });
            await handle.GetResultAsync();

            // Get handle by ID and RunId with known result type
            var handle2 = Client.GetActivityHandle<string>(activityId, handle.RunId);
            Assert.Equal(activityId, handle2.Id);
            Assert.Equal(handle.RunId, handle2.RunId);
            Assert.Equal("echo:test", await handle2.GetResultAsync());
        });
    }

    [Fact]
    public async Task DescribeAsync_RunningAndTerminated_IsAccurate()
    {
        await ExecuteActivityWorkerAsync(WaitForCancelAsync, async taskQueue =>
        {
            var activityId = $"act-{Guid.NewGuid()}";
            var handle = await Client.StartActivityAsync(
                () => WaitForCancelAsync(),
                new(activityId, taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                    StartToCloseTimeout = TimeSpan.FromMinutes(5),
                });

            // Describe while running
            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(ActivityExecutionStatus.Running, desc.Status);
                Assert.Equal(activityId, desc.ActivityId);
                Assert.Equal("WaitForCancel", desc.ActivityType);
                Assert.Equal(taskQueue, desc.TaskQueue);
                Assert.True(desc.ScheduledTime > DateTime.MinValue);
                Assert.Equal(1, desc.Attempt);
                Assert.NotNull(desc.ScheduleToCloseTimeout);
                Assert.NotNull(desc.StartToCloseTimeout);
                Assert.Null(desc.CloseTime);
            });

            // Terminate and describe again
            await handle.TerminateAsync();
            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(ActivityExecutionStatus.Terminated, desc.Status);
                Assert.NotNull(desc.CloseTime);
            });
        });
    }

    [Fact]
    public async Task DescribeAsync_UserMetadata_IsAccurate()
    {
        await ExecuteActivityWorkerAsync(SimpleActivityAsync, async taskQueue =>
        {
            var handle = await Client.StartActivityAsync(
                () => SimpleActivityAsync("meta"),
                new($"act-{Guid.NewGuid()}", taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                    StaticSummary = "Test summary",
                    StaticDetails = "Test details\nLine 2",
                });
            await handle.GetResultAsync();

            var desc = await handle.DescribeAsync();
            Assert.Equal("Test summary", await desc.GetStaticSummaryAsync());
            Assert.Equal("Test details\nLine 2", await desc.GetStaticDetailsAsync());
        });
    }

    [Fact]
    public async Task CancelAsync_RunningActivity_Succeeds()
    {
        waitForCancelReached = new TaskCompletionSource();
        try
        {
            await ExecuteActivityWorkerAsync(WaitForCancelAsync, async taskQueue =>
            {
                var handle = await Client.StartActivityAsync(
                    () => WaitForCancelAsync(),
                    new($"act-{Guid.NewGuid()}", taskQueue)
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                        HeartbeatTimeout = TimeSpan.FromSeconds(10),
                    });

                // Wait for the activity to actually start executing
                await waitForCancelReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

                // Cancel with reason
                await handle.CancelAsync(new() { Reason = "test cancel reason" });

                // Result should throw with cancellation inner
                var err = await Assert.ThrowsAsync<ActivityFailedException>(
                    () => handle.GetResultAsync());
                Assert.IsType<CanceledFailureException>(err.InnerException);

                // Describe should show canceled
                await AssertMore.EventuallyAsync(async () =>
                {
                    var desc = await handle.DescribeAsync();
                    Assert.Equal(ActivityExecutionStatus.Canceled, desc.Status);
                });
            });
        }
        finally
        {
            waitForCancelReached = null;
        }
    }

    [Fact]
    public async Task ListActivitiesAsync_SimpleList_IsAccurate()
    {
        await ExecuteActivityWorkerAsync(SimpleActivityAsync, async taskQueue =>
        {
            // Start and complete 5 activities
            for (var i = 0; i < 5; i++)
            {
                await Client.ExecuteActivityAsync(
                    () => SimpleActivityAsync($"item-{i}"),
                    new($"act-list-{Guid.NewGuid()}", taskQueue)
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                    });
            }

            // List and verify
            await AssertMore.EventuallyAsync(async () =>
            {
                var activities = new List<ActivityExecution>();
                await foreach (var act in Client.ListActivitiesAsync(
                    $"TaskQueue = '{taskQueue}'"))
                {
                    activities.Add(act);
                }
                Assert.Equal(5, activities.Count);
                foreach (var act in activities)
                {
                    Assert.Equal("SimpleActivity", act.ActivityType);
                    Assert.Equal(taskQueue, act.TaskQueue);
                    Assert.Equal(ActivityExecutionStatus.Completed, act.Status);
                }
            });

            // Verify count
            await AssertMore.EventuallyAsync(async () =>
            {
                var resp = await Client.CountActivitiesAsync(
                    $"TaskQueue = '{taskQueue}'");
                Assert.Equal(5, resp.Count);
            });

            // Verify manual paging
            await AssertMore.EventuallyAsync(async () =>
            {
                var options = new ActivityListPaginatedOptions { PageSize = 2 };
                var firstPage = await Client.ListActivitiesPaginatedAsync(
                    $"TaskQueue = '{taskQueue}'", null, options);
                Assert.Equal(2, firstPage.Activities.Count);
                Assert.NotNull(firstPage.NextPageToken);

                var secondPage = await Client.ListActivitiesPaginatedAsync(
                    $"TaskQueue = '{taskQueue}'", firstPage.NextPageToken, options);
                Assert.Equal(2, secondPage.Activities.Count);
                Assert.NotNull(secondPage.NextPageToken);

                var thirdPage = await Client.ListActivitiesPaginatedAsync(
                    $"TaskQueue = '{taskQueue}'", secondPage.NextPageToken, options);
                Assert.Equal(1, thirdPage.Activities.Count);
                Assert.Null(thirdPage.NextPageToken);
            });
        });
    }

    [Fact]
    public async Task StartActivityAsync_Interceptors_AreCalledProperly()
    {
        var interceptor = new ActivityTracingInterceptor();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new IClientInterceptor[] { interceptor };
        var client = new TemporalClient(Client.Connection, newOptions);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(
            client, new TemporalWorkerOptions(taskQueue).AddActivity(WaitForCancelAsync));
        await worker.ExecuteAsync(async () =>
        {
            var activityId = $"act-{Guid.NewGuid()}";
            var handle = await client.StartActivityAsync(
                () => WaitForCancelAsync(),
                new(activityId, taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(ActivityExecutionStatus.Running, desc.Status);
            });

            await handle.CancelAsync();
            await handle.TerminateAsync();

            Assert.Equal("StartActivity", interceptor.Events[0].Name);
            Assert.Equal(
                activityId,
                ((StartActivityInput)interceptor.Events[0].Input).Options.Id);

            Assert.Equal("DescribeActivity", interceptor.Events[1].Name);
            Assert.Equal(
                activityId,
                ((DescribeActivityInput)interceptor.Events[1].Input).Id);

            Assert.Equal("CancelActivity", interceptor.Events[^2].Name);
            Assert.Equal(
                activityId,
                ((CancelActivityInput)interceptor.Events[^2].Input).Id);

            Assert.Equal("TerminateActivity", interceptor.Events[^1].Name);
            Assert.Equal(
                activityId,
                ((TerminateActivityInput)interceptor.Events[^1].Input).Id);
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_WorkerActivityInfo_IsAccurate()
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(
            Client, new TemporalWorkerOptions(taskQueue).AddActivity(InspectInfoAsync));
        await worker.ExecuteAsync(async () =>
        {
            var activityId = $"act-{Guid.NewGuid()}";
            var info = await Client.ExecuteActivityAsync(
                () => InspectInfoAsync(),
                new(activityId, taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
                });

            Assert.Equal(activityId, info.ActivityId);
            Assert.Equal("InspectInfo", info.ActivityType);
            Assert.Equal(Client.Options.Namespace, info.Namespace);
            Assert.Equal(taskQueue, info.TaskQueue);
            Assert.False(info.IsLocal);
            Assert.False(info.IsWorkflowActivity);
            Assert.Null(info.WorkflowId);
            Assert.Null(info.WorkflowNamespace);
            Assert.Null(info.WorkflowRunId);
            Assert.Null(info.WorkflowType);
        });
    }

    public record ActivityInfoSnapshot(
        string ActivityId,
        string ActivityType,
        string Namespace,
        string TaskQueue,
        bool IsLocal,
        bool IsWorkflowActivity,
        string? WorkflowId,
        string? WorkflowNamespace,
        string? WorkflowRunId,
        string? WorkflowType);

    [Activity]
    public static Task<ActivityInfoSnapshot> InspectInfoAsync()
    {
        var info = ActivityExecutionContext.Current.Info;
        return Task.FromResult(new ActivityInfoSnapshot(
            ActivityId: info.ActivityId,
            ActivityType: info.ActivityType,
            Namespace: info.Namespace,
            TaskQueue: info.TaskQueue,
            IsLocal: info.IsLocal,
            IsWorkflowActivity: info.IsWorkflowActivity,
            WorkflowId: info.WorkflowId,
            WorkflowNamespace: info.WorkflowNamespace,
            WorkflowRunId: info.WorkflowRunId,
            WorkflowType: info.WorkflowType));
    }

    internal record TracingEvent(string Name, object Input);

    internal class ActivityTracingInterceptor : IClientInterceptor
    {
        public List<TracingEvent> Events { get; } = new();

        public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
            new ActivityTracingOutboundInterceptor(next, Events);
    }

    internal class ActivityTracingOutboundInterceptor : ClientOutboundInterceptor
    {
        public ActivityTracingOutboundInterceptor(
            ClientOutboundInterceptor next, List<TracingEvent> events)
            : base(next)
        {
            Events = events;
        }

        public List<TracingEvent> Events { get; private init; }

        public override Task<ActivityHandle<TResult>> StartActivityAsync<TResult>(
            StartActivityInput input)
        {
            Events.Add(new("StartActivity", input));
            return base.StartActivityAsync<TResult>(input);
        }

        public override Task<ActivityExecutionDescription> DescribeActivityAsync(
            DescribeActivityInput input)
        {
            Events.Add(new("DescribeActivity", input));
            return base.DescribeActivityAsync(input);
        }

        public override Task CancelActivityAsync(CancelActivityInput input)
        {
            Events.Add(new("CancelActivity", input));
            return base.CancelActivityAsync(input);
        }

        public override Task TerminateActivityAsync(TerminateActivityInput input)
        {
            Events.Add(new("TerminateActivity", input));
            return base.TerminateActivityAsync(input);
        }
    }

    private async Task ExecuteActivityWorkerAsync(
        Delegate activity, Func<string, Task> testFunc)
    {
        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(
            Client, new TemporalWorkerOptions(taskQueue).AddActivity(activity));
        await worker.ExecuteAsync(() => testFunc(taskQueue));
    }
}
