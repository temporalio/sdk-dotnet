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
    public async Task StartActivityAsync_NegativeStartDelay_Throws()
    {
        var err = await Assert.ThrowsAsync<ArgumentException>(() =>
            Client.StartActivityAsync(
                () => SimpleActivityAsync("test"),
                new($"act-{Guid.NewGuid()}", $"tq-{Guid.NewGuid()}")
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(5),
                    StartDelay = TimeSpan.FromSeconds(-1),
                }));
        Assert.Contains("StartDelay must be non-negative", err.Message);
    }

    [Fact]
    public async Task StartActivityAsync_StartDelay_WaitsProperly()
    {
        await ExecuteActivityWorkerAsync(SimpleActivityAsync, async taskQueue =>
        {
            var startDelay = TimeSpan.FromSeconds(2);
            var handle = await Client.StartActivityAsync(
                () => SimpleActivityAsync("delayed"),
                new($"act-{Guid.NewGuid()}", taskQueue)
                {
                    // ScheduleToCloseTimeout = TimeSpan.FromSeconds(36),
                    StartToCloseTimeout = TimeSpan.FromSeconds(5),
                    StartDelay = startDelay,
                });

            Assert.Equal("echo:delayed", await handle.GetResultAsync());

            var desc = await handle.DescribeAsync();
            Assert.Equal(ActivityExecutionStatus.Completed, desc.Status);
            Assert.True(desc.ScheduledTime > DateTime.MinValue);
            Assert.NotNull(desc.LastStartedTime);
            Assert.True(
                desc.LastStartedTime.Value - desc.ScheduledTime >= startDelay - TimeSpan.FromMilliseconds(500));
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
    [CloudTestExclusion(
        CloudTestExclusionReason.NeedsCloudAdaptation,
        "Cloud visibility may return a next-page token that resolves to an empty final page.")]
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
                Assert.Single(thirdPage.Activities);
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
                    HeartbeatTimeout = TimeSpan.FromSeconds(10),
                });

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(ActivityExecutionStatus.Running, desc.Status);
            });

            await handle.CancelAsync();

            var err = await Assert.ThrowsAsync<ActivityFailedException>(
                () => handle.GetResultAsync());
            Assert.IsType<CanceledFailureException>(err.InnerException);

            Assert.Equal("StartActivity", interceptor.Events[0].Name);
            Assert.Equal(
                activityId,
                ((StartActivityInput)interceptor.Events[0].Input).Options.Id);

            Assert.Equal("DescribeActivity", interceptor.Events[1].Name);
            Assert.Equal(
                activityId,
                ((DescribeActivityInput)interceptor.Events[1].Input).Id);

            Assert.Equal("CancelActivity", interceptor.Events[2].Name);
            Assert.Equal(
                activityId,
                ((CancelActivityInput)interceptor.Events[2].Input).Id);
        });
    }

    [Fact]
    public async Task TerminateActivityAsync_Interceptor_IsCalledProperly()
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
                    HeartbeatTimeout = TimeSpan.FromSeconds(10),
                });

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(ActivityExecutionStatus.Running, desc.Status);
            });

            await handle.TerminateAsync();

            var err = await Assert.ThrowsAsync<ActivityFailedException>(
                () => handle.GetResultAsync());
            Assert.IsType<TerminatedFailureException>(err.InnerException);

            Assert.Equal("StartActivity", interceptor.Events[0].Name);
            Assert.Equal(
                activityId,
                ((StartActivityInput)interceptor.Events[0].Input).Options.Id);

            Assert.Equal("DescribeActivity", interceptor.Events[1].Name);
            Assert.Equal(
                activityId,
                ((DescribeActivityInput)interceptor.Events[1].Input).Id);

            Assert.Equal("TerminateActivity", interceptor.Events[2].Name);
            Assert.Equal(
                activityId,
                ((TerminateActivityInput)interceptor.Events[2].Input).Id);
        });
    }

    [Fact]
    public async Task PauseActivityAsync_Interceptor_IsCalledProperly()
    {
        var interceptor = new ActivityTracingInterceptor();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new IClientInterceptor[] { interceptor };
        var client = new TemporalClient(Client.Connection, newOptions);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var activityId = $"act-{Guid.NewGuid()}";
        var handle = await client.StartActivityAsync(
            () => VoidActivityAsync(),
            new(activityId, taskQueue)
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
            });

        try
        {
            await handle.PauseAsync(new() { Reason = "Pause reason" });

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(ActivityExecutionStatus.Paused, desc.Status);
            });

            await handle.UnpauseAsync(new() { Reason = "Unpause reason" });

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(ActivityExecutionStatus.Running, desc.Status);
            });

            Assert.Equal("StartActivity", interceptor.Events[0].Name);
            Assert.Equal(
                activityId,
                ((StartActivityInput)interceptor.Events[0].Input).Options.Id);

            Assert.Equal("PauseActivity", interceptor.Events[1].Name);
            Assert.Equal(
                activityId,
                ((PauseActivityInput)interceptor.Events[1].Input).Id);
            Assert.Equal(
                "Pause reason",
                ((PauseActivityInput)interceptor.Events[1].Input).Options?.Reason);

            Assert.Equal("DescribeActivity", interceptor.Events[2].Name);
            Assert.Equal(
                activityId,
                ((DescribeActivityInput)interceptor.Events[2].Input).Id);

            Assert.Equal("UnpauseActivity", interceptor.Events[3].Name);
            Assert.Equal(
                activityId,
                ((UnpauseActivityInput)interceptor.Events[3].Input).Id);
            Assert.Equal(
                "Unpause reason",
                ((UnpauseActivityInput)interceptor.Events[3].Input).Options?.Reason);

            Assert.Equal("DescribeActivity", interceptor.Events[4].Name);
            Assert.Equal(
                activityId,
                ((DescribeActivityInput)interceptor.Events[4].Input).Id);
        }
        finally
        {
            await handle.TerminateAsync();
        }
    }

    [Fact]
    public async Task UpdateActivityOptionsAsync_Interceptor_IsCalledProperly()
    {
        var interceptor = new ActivityTracingInterceptor();
        var newOptions = (TemporalClientOptions)Client.Options.Clone();
        newOptions.Interceptors = new IClientInterceptor[] { interceptor };
        var client = new TemporalClient(Client.Connection, newOptions);

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var activityId = $"act-{Guid.NewGuid()}";
        var handle = await client.StartActivityAsync(
            () => VoidActivityAsync(),
            new(activityId, taskQueue)
            {
                ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
            });

        try
        {
            await handle.UpdateOptionsAsync(new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(10) });

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(TimeSpan.FromMinutes(10), desc.ScheduleToCloseTimeout);
            });

            await handle.RestoreOriginalOptionsAsync();

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(TimeSpan.FromMinutes(5), desc.ScheduleToCloseTimeout);
            });

            Assert.Equal("StartActivity", interceptor.Events[0].Name);
            Assert.Equal(
                activityId,
                ((StartActivityInput)interceptor.Events[0].Input).Options.Id);

            Assert.Equal("UpdateActivityOptions", interceptor.Events[1].Name);
            Assert.Equal(
                activityId,
                ((UpdateActivityOptionsInput)interceptor.Events[1].Input).Id);

            Assert.Equal("DescribeActivity", interceptor.Events[2].Name);
            Assert.Equal(
                activityId,
                ((DescribeActivityInput)interceptor.Events[2].Input).Id);

            Assert.Equal("RestoreOriginalActivityOptions", interceptor.Events[3].Name);
            Assert.Equal(
                activityId,
                ((RestoreOriginalActivityOptionsInput)interceptor.Events[3].Input).Id);

            Assert.Equal("DescribeActivity", interceptor.Events[4].Name);
            Assert.Equal(
                activityId,
                ((DescribeActivityInput)interceptor.Events[4].Input).Id);
        }
        finally
        {
            // Cleanup
            await handle.TerminateAsync();
        }
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

    [Fact]
    public async Task UpdateActivityOptionsAsync_UpdatesCorrectly()
    {
        var originalTaskQueue = $"tq-{Guid.NewGuid()}";
        var updatedTaskQueue = originalTaskQueue + "-updated";

        var originalTimeSpan = TimeSpan.FromMinutes(5);
        var firstUpdateTimeSpan = TimeSpan.FromMinutes(10);
        var secondUpdateTimeSpan = TimeSpan.FromMinutes(15);

        StartActivityOptions startOptions = new($"act-{Guid.NewGuid()}", originalTaskQueue)
        {
            ScheduleToCloseTimeout = originalTimeSpan,
            ScheduleToStartTimeout = originalTimeSpan,
            StartToCloseTimeout = originalTimeSpan,
            HeartbeatTimeout = originalTimeSpan,
            Priority = new(fairnessKey: "original"),
            RetryPolicy = new() { InitialInterval = originalTimeSpan },
            StartDelay = originalTimeSpan,
        };

        var handle = await Client.StartActivityAsync(() => VoidActivityAsync(), startOptions);

        try
        {
            var firstUpdateResult = await handle.UpdateOptionsAsync(new()
            {
                TaskQueue = updatedTaskQueue,
                ScheduleToCloseTimeout = firstUpdateTimeSpan,
                ScheduleToStartTimeout = firstUpdateTimeSpan,
                StartToCloseTimeout = firstUpdateTimeSpan,
                HeartbeatTimeout = firstUpdateTimeSpan,
                Priority = new(fairnessKey: "first update"),
                RetryPolicy = new() { InitialInterval = firstUpdateTimeSpan },
                StartDelay = firstUpdateTimeSpan,
            });
            Assert.Equal(updatedTaskQueue, firstUpdateResult.TaskQueue);
            Assert.Equal(firstUpdateTimeSpan, firstUpdateResult.ScheduleToCloseTimeout);
            Assert.Equal(firstUpdateTimeSpan, firstUpdateResult.ScheduleToStartTimeout);
            Assert.Equal(firstUpdateTimeSpan, firstUpdateResult.StartToCloseTimeout);
            Assert.Equal(firstUpdateTimeSpan, firstUpdateResult.HeartbeatTimeout);
            Assert.Equal("first update", firstUpdateResult.Priority?.FairnessKey);
            Assert.Equal(firstUpdateTimeSpan, firstUpdateResult.RetryPolicy?.InitialInterval);
            Assert.Equal(firstUpdateTimeSpan, firstUpdateResult.StartDelay);

            await AssertMore.EventuallyAsync(async () =>
            {
                var desc = await handle.DescribeAsync();
                Assert.Equal(updatedTaskQueue, desc.TaskQueue);
                Assert.Equal(firstUpdateTimeSpan, desc.ScheduleToCloseTimeout);
                Assert.Equal(firstUpdateTimeSpan, desc.ScheduleToStartTimeout);
                Assert.Equal(firstUpdateTimeSpan, desc.StartToCloseTimeout);
                Assert.Equal(firstUpdateTimeSpan, desc.HeartbeatTimeout);
                // Assert.Equal("updated", desc.Priority?.FairnessKey); // TODO: Uncomment when property added
                Assert.Equal(firstUpdateTimeSpan, desc.RetryPolicy?.InitialInterval);
                // Assert.Equal(firstUpdateTimeSpan, desc.StartDelay); // TODO: Uncomment when property added
            });

            ActivityOptionsUpdate secondUpdate = new();
            // Task queue implicitly null
            secondUpdate.ScheduleToCloseTimeout = secondUpdateTimeSpan;
            secondUpdate.ScheduleToStartTimeout = null;
            secondUpdate.StartToCloseTimeout = secondUpdateTimeSpan;
            secondUpdate.StartToCloseTimeout = null; // should not update
            secondUpdate.ClearHeartbeatTimeout = true;
            secondUpdate.Priority = new(fairnessKey: "second update");
            secondUpdate.ClearPriority = true;
            secondUpdate.ClearPriority = false; // should not update
            secondUpdate.RetryPolicy = new() { MaximumInterval = secondUpdateTimeSpan }; // should reset InitialInterval
            secondUpdate.ClearStartDelay = true;
            secondUpdate.StartDelay = secondUpdateTimeSpan; // should update

            var secondUpdateResult = await handle.UpdateOptionsAsync(secondUpdate);
            Assert.Equal(updatedTaskQueue, secondUpdateResult.TaskQueue);
            Assert.Equal(secondUpdateTimeSpan, secondUpdateResult.ScheduleToCloseTimeout);
            Assert.Equal(firstUpdateTimeSpan, secondUpdateResult.ScheduleToStartTimeout);
            Assert.Equal(firstUpdateTimeSpan, secondUpdateResult.StartToCloseTimeout);
            Assert.Null(secondUpdateResult.HeartbeatTimeout);
            Assert.Equal("first update", secondUpdateResult.Priority?.FairnessKey);
            Assert.NotEqual(firstUpdateTimeSpan, secondUpdateResult.RetryPolicy?.InitialInterval);
            Assert.Equal(secondUpdateTimeSpan, secondUpdateResult.RetryPolicy?.MaximumInterval);
            Assert.Equal(secondUpdateTimeSpan, secondUpdateResult.StartDelay);

            var restoreResult = await handle.RestoreOriginalOptionsAsync();

            Assert.Equal(originalTaskQueue, restoreResult.TaskQueue);
            Assert.Equal(originalTimeSpan, restoreResult.ScheduleToCloseTimeout);
            Assert.Equal(originalTimeSpan, restoreResult.ScheduleToStartTimeout);
            Assert.Equal(originalTimeSpan, restoreResult.StartToCloseTimeout);
            Assert.Equal(originalTimeSpan, restoreResult.HeartbeatTimeout);
            Assert.Equal("original", restoreResult.Priority?.FairnessKey);
            Assert.Equal(originalTimeSpan, restoreResult.RetryPolicy?.InitialInterval);
            Assert.NotEqual(secondUpdateTimeSpan, restoreResult.RetryPolicy?.MaximumInterval);
            Assert.Equal(originalTimeSpan, restoreResult.StartDelay);
        }
        finally
        {
            // Cleanup
            await handle.TerminateAsync();
        }
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

        public override Task PauseActivityAsync(PauseActivityInput input)
        {
            Events.Add(new("PauseActivity", input));
            return base.PauseActivityAsync(input);
        }

        public override Task UnpauseActivityAsync(UnpauseActivityInput input)
        {
            Events.Add(new("UnpauseActivity", input));
            return base.UnpauseActivityAsync(input);
        }

        public override Task<ActivityOptionsUpdate> UpdateActivityOptionsAsync(UpdateActivityOptionsInput input)
        {
            Events.Add(new("UpdateActivityOptions", input));
            return base.UpdateActivityOptionsAsync(input);
        }

        public override Task<ActivityOptionsUpdate> RestoreOriginalActivityOptionsAsync(RestoreOriginalActivityOptionsInput input)
        {
            Events.Add(new("RestoreOriginalActivityOptions", input));
            return base.RestoreOriginalActivityOptionsAsync(input);
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
