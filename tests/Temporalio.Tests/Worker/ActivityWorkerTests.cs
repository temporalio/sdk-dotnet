namespace Temporalio.Tests.Worker;

using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Xunit;
using Xunit.Abstractions;

public class ActivityWorkerTests : WorkflowEnvironmentTestBase
{
    private readonly string instanceState1 = "InstanceState1";
    private string instanceState2 = "InstanceState2";
    private string instanceState3 = "InstanceState3";

    public ActivityWorkerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleStaticMethod_Succeeds()
    {
        Assert.Equal("param: 123", await ExecuteActivityAsync(SimpleStaticMethod, 123));
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleInstanceMethod_Succeeds()
    {
        Assert.Equal(
            new Dictionary<string, List<string>>() { ["InstanceState1"] = new() { "foo", "bar" } },
            await ExecuteActivityAsync(SimpleInstanceMethod, new List<string>() { "foo", "bar" }));
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleVoidMethod_Succeeds()
    {
        await ExecuteActivityAsync(SimpleVoidMethod, "-mutated");
        Assert.Equal("InstanceState2-mutated", instanceState2);
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleAsyncMethod_Succeeds()
    {
        Assert.Equal(
            new List<string>() { "foo:param", "bar:param" },
            await ExecuteActivityAsync(SimpleMethodAsync, "param"));
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleAsyncVoidMethod_Succeeds()
    {
        await ExecuteActivityAsync(SimpleVoidMethodAsync, "-mutated");
        Assert.Equal("InstanceState3-mutated", instanceState3);
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleLambda_Succeeds()
    {
        var activity =
            [Activity("SimpleLambda")] (string param) =>
                new List<string> { $"foo:{param}", $"bar:{param}" };
        Assert.Equal(
            new List<string>() { "foo:param", "bar:param" },
            await ExecuteActivityAsync(activity, "param"));
    }

    [Fact]
    public async Task ExecuteActivityAsync_SimpleGenericMethod_Succeeds()
    {
        var ran = false;
        [Activity]
        Task<T> DoThingAsync<T>(T arg)
        {
            ran = true;
            return Task.FromResult(arg);
        }
        Assert.Equal("foo", await ExecuteActivityAsync(DoThingAsync<string>, "foo"));
        Assert.True(ran);
    }

    [Fact]
    public async Task ExecuteActivityAsync_CheckInfo_IsAccurate()
    {
        [Activity]
        static ActivityInfo GetInfo() => ActivityContext.Current.Info;
        var info = await ExecuteActivityAsync(GetInfo);
        // Just assert some values for now
        var beforeNow = DateTime.UtcNow.AddSeconds(-30);
        var afterNow = DateTime.UtcNow.AddSeconds(30);
        Assert.Equal("GetInfo", info.ActivityType);
        Assert.Equal(1, info.Attempt);
        Assert.InRange(info.CurrentAttemptScheduledTime, beforeNow, afterNow);
        Assert.False(info.IsLocal);
    }

    [Fact]
    public async Task ExecuteActivityAsync_ThrowCommonException_ReportsFailure()
    {
        [Activity]
        static void Throw() => throw new InvalidOperationException("Oh no");
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
            async () => await ExecuteActivityAsync(Throw));
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        var appErr = Assert.IsType<ApplicationFailureException>(actErr.InnerException);
        Assert.Equal("Oh no", appErr.Message);
        Assert.Equal("InvalidOperationException", appErr.ErrorType);
    }

    [Fact]
    public async Task ExecuteActivityAsync_ThrowApplicationFailureException_ReportsFailure()
    {
        [Activity]
        static void Throw() => throw new ApplicationFailureException(
            "Some message", errorType: "SomeType", details: new string[] { "foo", "bar" });
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
            async () => await ExecuteActivityAsync(Throw));
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        var appErr = Assert.IsType<ApplicationFailureException>(actErr.InnerException);
        Assert.Equal("Some message", appErr.Message);
        Assert.Equal("SomeType", appErr.ErrorType);
        Assert.Equal(2, appErr.Details.Count);
        Assert.Equal("foo", appErr.Details.ElementAt<string>(0));
        Assert.Equal("bar", appErr.Details.ElementAt<string>(1));
    }

    [Fact]
    public async Task ExecuteActivityAsync_BadParamConversion_ReportsFailure()
    {
        [Activity]
        static string BadParam(string param) => param;
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
            async () => await ExecuteActivityAsync<string>(BadParam, 123));
        Assert.Contains("Failed decoding parameters", wfErr.ToString());
    }

    [Fact]
    public async Task ExecuteActivityAsync_CalledWithTooFewParams_ReportsFailure()
    {
        [Activity]
        static string TwoParam(int param1, int param2) => string.Empty;
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
            async () => await ExecuteActivityAsync<string>(TwoParam, 123));
        Assert.Contains("more than that are required by the signature", wfErr.ToString());
    }

    [Fact]
    public async Task ExecuteActivityAsync_CalledWithoutDefaultParams_UsesDefaults()
    {
        [Activity]
        static string DefaultParam(string param1, string param2 = "param2") => $"{param1}:{param2}";
        Assert.Equal("param1:param2", await ExecuteActivityAsync<string>(DefaultParam, "param1"));
        Assert.Equal(
            "param1:param3",
            await ExecuteActivityAsync<string>(DefaultParam, args: new object[] { "param1", "param3" }));
    }

    [Fact]
    public async Task ExecuteActivityAsync_CalledWithTooManyParams_IgnoresExtra()
    {
        [Activity]
        static string OneParam(string param1) => param1;
        Assert.Equal(
            "param1",
            await ExecuteActivityAsync<string>(OneParam, args: new object[] { "param1", "param2" }));
    }

    [Fact]
    public async Task ExecuteActivityAsync_SentCancel_ReportsCancel()
    {
        var activityReached = new TaskCompletionSource();
        var gotCancellation = false;
        [Activity]
        async Task WaitUntilCancelledAsync()
        {
            activityReached.SetResult();
            while (!ActivityContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityContext.Current.Heartbeat();
                await Task.Delay(300);
            }
            gotCancellation = true;
            ActivityContext.Current.CancellationToken.ThrowIfCancellationRequested();
        }
        await Assert.ThrowsAsync<WorkflowFailedException>(() => ExecuteActivityAsync(
            WaitUntilCancelledAsync,
            waitForCancellation: true,
            heartbeatTimeout: TimeSpan.FromSeconds(1),
            afterStarted: async handle =>
            {
                // Wait for activity to be reached, then cancel the workflow
                await activityReached.Task.WaitAsync(TimeSpan.FromSeconds(20));
                await handle.CancelAsync();
            }));
        Assert.True(gotCancellation);
    }

    [Fact]
    public async Task ExecuteActivityAsync_CaughtCancel_Succeeds()
    {
        var activityReached = new TaskCompletionSource();
        [Activity]
        async Task<string> CatchCancelledAsync()
        {
            activityReached.SetResult();
            while (!ActivityContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityContext.Current.Heartbeat();
                await Task.Delay(300);
            }
            return "Cancelled!";
        }
        var res = await ExecuteActivityAsync(
            CatchCancelledAsync,
            waitForCancellation: true,
            heartbeatTimeout: TimeSpan.FromSeconds(1),
            afterStarted: async handle =>
            {
                // Wait for activity to be reached, then cancel the workflow
                await activityReached.Task.WaitAsync(TimeSpan.FromSeconds(20));
                await handle.CancelAsync();
            });
        Assert.Equal("Cancelled!", res);
    }

    [Fact]
    public async Task ExecuteActivityAsync_WorkerShutdown_ReportsFailure()
    {
        using var workerStoppingSource = new CancellationTokenSource();
        var activityReached = new TaskCompletionSource();
        var gotCancellation = false;
        [Activity]
        async Task WaitUntilCancelledAsync()
        {
            activityReached.SetResult();
            while (!ActivityContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityContext.Current.Heartbeat();
                await Task.Delay(300);
            }
            gotCancellation = true;
            ActivityContext.Current.CancellationToken.ThrowIfCancellationRequested();
        }
        var workflowID = string.Empty;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExecuteActivityAsync(
            WaitUntilCancelledAsync,
            workerStoppingToken: workerStoppingSource.Token,
            afterStarted: async handle =>
            {
                workflowID = handle.ID;
                // Wait for activity to be reached, then stop the worker
                await activityReached.Task.WaitAsync(TimeSpan.FromSeconds(20));
                workerStoppingSource.Cancel();
            }));
        Assert.True(gotCancellation);
        // Check the workflow error
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
            () => Client.GetWorkflowHandle(workflowID).GetResultAsync());
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        Assert.IsType<ApplicationFailureException>(actErr.InnerException);
    }

    [Fact]
    public async Task ExecuteActivityAsync_ThrowsOperationCancelled_ReportsFailure()
    {
        // Just to confirm that a .NET cancelled exception when cancel is not requested is properly
        // treated as an app exception instead of marking activity cancelled
        [Activity]
        static void Throws() => throw new OperationCanceledException();
        var wfErr = await Assert.ThrowsAnyAsync<WorkflowFailedException>(
            () => ExecuteActivityAsync(Throws));
        // Check the workflow error
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        Assert.IsType<ApplicationFailureException>(actErr.InnerException);
    }

    [Fact]
    public async Task ExecuteActivityAsync_UnknownActivity_ReportsFailure()
    {
        [Activity]
        static void Ignored() => throw new NotImplementedException();
        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { Ignored },
        });
        await worker.ExecuteAsync(async () =>
        {
            var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => Env.Client.ExecuteWorkflowAsync(
                    IKitchenSinkWorkflow.Ref.RunAsync,
                    new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                        Name: "ActivityDoesNotExist",
                        TaskQueue: taskQueue))),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)));
            var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
            var appErr = Assert.IsType<ApplicationFailureException>(actErr.InnerException);
            Assert.Contains(
                "not registered on this worker, available activities: Ignored", appErr.Message);
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_MaxConcurrent_TimesOutIfMore()
    {
        [Activity]
        static Task WaitUntilCancelledAsync() =>
            Task.Delay(Timeout.Infinite, ActivityContext.Current.CancellationToken);
        // Only allow 5 activities but try to execute 6 and confirm schedule to start timeout fails
        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { WaitUntilCancelledAsync },
            MaxConcurrentActivities = 5,
        });
        await worker.ExecuteAsync(async () =>
        {
            var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => Env.Client.ExecuteWorkflowAsync(
                    IKitchenSinkWorkflow.Ref.RunAsync,
                    new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                        Name: "WaitUntilCancelled",
                        TaskQueue: taskQueue,
                        Count: 6,
                        ScheduleToStartTimeoutMS: 1000))),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue)));
            var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
            var toErr = Assert.IsType<TimeoutFailureException>(actErr.InnerException);
            Assert.Equal(TimeoutType.ScheduleToStart, toErr.TimeoutType);
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_HeartbeatTimeout_ReportsFailure()
    {
        [Activity]
        static Task WaitUntilCancelledAsync() =>
            Task.Delay(Timeout.Infinite, ActivityContext.Current.CancellationToken);
        var wfErr = await Assert.ThrowsAnyAsync<WorkflowFailedException>(() => ExecuteActivityAsync(
            WaitUntilCancelledAsync,
            heartbeatTimeout: TimeSpan.FromSeconds(1)));
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        var toErr = Assert.IsType<TimeoutFailureException>(actErr.InnerException);
        Assert.Equal(TimeoutType.Heartbeat, toErr.TimeoutType);
    }

    [Fact]
    public async Task ExecuteActivityAsync_HeartbeatDetailsConversionFailure_ReportsFailure()
    {
        var cancelReason = ActivityCancelReason.None;
        [Activity]
        async Task BadHeartbeatDetailsAsync()
        {
            while (!ActivityContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityContext.Current.Heartbeat(() => "can't serialize me!");
                await Task.Delay(100);
            }
            cancelReason = ActivityContext.Current.CancelReason;
            ActivityContext.Current.CancellationToken.ThrowIfCancellationRequested();
        }
        await Assert.ThrowsAnyAsync<WorkflowFailedException>(() => ExecuteActivityAsync(
            BadHeartbeatDetailsAsync));
        Assert.Equal(ActivityCancelReason.HeartbeatRecordFailure, cancelReason);
    }

    [Fact]
    public async Task ExecuteActivityAsync_HeartbeatDetailsAfterFailure_ProperlyRecorded()
    {
        var heartbeatDetail = "<unset>";
        [Activity]
        async Task HeartbeatAndFailAsync()
        {
            ActivityContext.Current.Heartbeat($"attempt: {ActivityContext.Current.Info.Attempt}");
            if (ActivityContext.Current.Info.HeartbeatDetails.Count > 0)
            {
                heartbeatDetail = await ActivityContext.Current.Info.HeartbeatDetailAtAsync<string>(0);
            }
            throw new InvalidOperationException("Oh no");
        }
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(() => ExecuteActivityAsync(
            HeartbeatAndFailAsync, maxAttempts: 2));
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        var appErr = Assert.IsType<ApplicationFailureException>(actErr.InnerException);
        Assert.Equal("Oh no", appErr.Message);
        // Should show the detail from the first attempt
        Assert.Equal("attempt: 1", heartbeatDetail);
    }

    [Fact]
    public async Task ExecuteActivityAsync_ManualDefinition_Succeeds()
    {
        static string SayHello(string name) => $"Hello, {name}!";
        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            AdditionalActivityDefinitions =
            {
                ActivityDefinition.CreateWithoutAttribute("my-activity", SayHello),
            },
        });
        await worker.ExecuteAsync(async () =>
        {
            // Run workflow
            var result = await Env.Client.ExecuteWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                    Name: "my-activity",
                    Args: new[] { "Temporal" },
                    TaskQueue: taskQueue))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_AsyncCompletion_Succeeds()
    {
        var taskTokenCompletion = new TaskCompletionSource<byte[]>();
        [Activity]
        void CompleteExternal()
        {
            taskTokenCompletion.SetResult(ActivityContext.Current.Info.TaskToken);
            throw new CompleteAsyncException();
        }

        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { CompleteExternal },
        });
        await worker.ExecuteAsync(async () =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                    Name: "CompleteExternal",
                    TaskQueue: taskQueue))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

            // Wait for task token
            var taskToken = await taskTokenCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Send completion
            await Env.Client.GetAsyncActivityHandle(taskToken).CompleteAsync("Yay completed");
            Assert.Equal("Yay completed", await handle.GetResultAsync<string>());
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_AsyncCompletionHeartbeatAndFail_ProperlyRecorded()
    {
        var taskTokenCompletion = new TaskCompletionSource<byte[]>();
        [Activity]
        void CompleteExternal()
        {
            taskTokenCompletion.SetResult(ActivityContext.Current.Info.TaskToken);
            throw new CompleteAsyncException();
        }

        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { CompleteExternal },
        });
        await worker.ExecuteAsync(async () =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                    Name: "CompleteExternal",
                    TaskQueue: taskQueue))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

            // Wait for task token
            var taskToken = await taskTokenCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Send heartbeat and confirm details accurate
            var actHandle = Env.Client.GetAsyncActivityHandle(taskToken);
            await actHandle.HeartbeatAsync(
                new() { Details = new object[] { "foo", "bar" } });
            var desc = await handle.DescribeAsync();
            var det = desc.RawDescription.PendingActivities[0].HeartbeatDetails.Payloads_;
            Assert.Equal("foo", await DataConverter.Default.ToValueAsync<string>(det[0]));
            Assert.Equal("bar", await DataConverter.Default.ToValueAsync<string>(det[1]));

            // Send failure and confirm accurate
            await actHandle.FailAsync(new InvalidOperationException("Oh no"));
            var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
            var appErr = Assert.IsType<ApplicationFailureException>(actErr.InnerException);
            Assert.Equal("Oh no", appErr.Message);
            Assert.Equal("InvalidOperationException", appErr.ErrorType);
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_AsyncCompletionCancel_ReportsCancel()
    {
        var taskTokenCompletion = new TaskCompletionSource<byte[]>();
        [Activity]
        void CompleteExternal()
        {
            taskTokenCompletion.SetResult(ActivityContext.Current.Info.TaskToken);
            throw new CompleteAsyncException();
        }

        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { CompleteExternal },
        });
        await worker.ExecuteAsync(async () =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                    Name: "CompleteExternal",
                    TaskQueue: taskQueue,
                    WaitForCancellation: true))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

            // Wait for task token
            var taskToken = await taskTokenCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Cancel workflow then confirm activity wants to be cancelled
            var actHandle = Env.Client.GetAsyncActivityHandle(taskToken);
            await handle.CancelAsync();
            await AssertMore.EqualEventuallyAsync(true, async () =>
            {
                try
                {
                    await actHandle.HeartbeatAsync();
                    return false;
                }
                catch (AsyncActivityCancelledException)
                {
                    return true;
                }
            });

            // Send cancel and confirm cancelled
            await actHandle.ReportCancellationAsync();
            var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
                async () => await handle.GetResultAsync());
            Assert.IsType<CancelledFailureException>(wfErr.InnerException);
        });
    }

    [Fact]
    public async Task ExecuteActivityAsync_AsyncCompletionStartToCloseTimeout_ReportsCancel()
    {
        var taskTokenCompletion = new TaskCompletionSource<byte[]>();
        [Activity]
        void CompleteExternal()
        {
            taskTokenCompletion.SetResult(ActivityContext.Current.Info.TaskToken);
            throw new CompleteAsyncException();
        }

        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { CompleteExternal },
        });
        await worker.ExecuteAsync(async () =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                    Name: "CompleteExternal",
                    TaskQueue: taskQueue,
                    WaitForCancellation: true,
                    StartToCloseTimeoutMS: 1000))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

            // Wait for task token
            var taskToken = await taskTokenCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Wait for heartbeat to show not found
            var actHandle = Env.Client.GetAsyncActivityHandle(taskToken);
            await AssertMore.EqualEventuallyAsync(true, async () =>
            {
                try
                {
                    await actHandle.HeartbeatAsync();
                    return false;
                }
                catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)
                {
                    return true;
                }
            });
        });
    }

    [Fact]
    public async Task ExecuteAsync_PollFailure_ShutsDownWorker()
    {
        var activityWaiting = new TaskCompletionSource();
        var workerShutdown = false;
        [Activity]
        async Task WaitUntilCancelledAsync()
        {
            activityWaiting.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ActivityContext.Current.CancellationToken);
            }
            catch (TaskCanceledException) when (
                ActivityContext.Current.CancelReason == ActivityCancelReason.WorkerShutdown)
            {
                workerShutdown = true;
                throw;
            }
        }

        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { WaitUntilCancelledAsync },
        });
        // Overwrite bridge worker with one we can inject failures on
        var bridgeWorker = new ManualPollCompletionBridgeWorker(worker.BridgeWorker);
        worker.BridgeWorker = bridgeWorker;

        // Run the worker
        WorkflowHandle? handle = null;
        var wErr = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await worker.ExecuteAsync(async () =>
        {
            // Start the workflow
            handle = await Env.Client.StartWorkflowAsync(
                IKitchenSinkWorkflow.Ref.RunAsync,
                new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                    Name: "WaitUntilCancelled",
                    TaskQueue: taskQueue))),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));

            // Wait for activity to report waiting, then send a poll failure
            await activityWaiting.Task;
            bridgeWorker.PollActivityCompletion.SetException(
                new InvalidOperationException("Oh no"));
            await Task.Delay(Timeout.Infinite);
        }));
        Assert.Equal("Oh no", wErr.Message);

        // Confirm workflow cancelled
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
            () => handle!.GetResultAsync());
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        var appErr = Assert.IsType<ApplicationFailureException>(actErr.InnerException);
        Assert.Equal("TaskCanceledException", appErr.ErrorType);
        Assert.True(workerShutdown);
    }

    [Fact]
    public void New_DuplicateActivityNames_Throws()
    {
        [Activity("some-activity")]
        static string SomeActivity1() => string.Empty;
        [Activity("some-activity")]
        static string SomeActivity2() => string.Empty;
        var err = Assert.Throws<ArgumentException>(() =>
        {
            using var worker = new TemporalWorker(Client, new()
            {
                TaskQueue = $"tq-{Guid.NewGuid()}",
                Activities = { SomeActivity1, SomeActivity2 },
            });
        });
        Assert.Equal("Duplicate activity named some-activity", err.Message);
    }

    [Activity]
    internal static string SimpleStaticMethod(int param)
    {
        return $"param: {param}";
    }

    [Activity]
    internal Dictionary<string, List<string>> SimpleInstanceMethod(List<string> someStrings)
    {
        return new() { [instanceState1] = someStrings };
    }

    [Activity]
    internal void SimpleVoidMethod(string someSuffix)
    {
        instanceState2 += someSuffix;
    }

    [Activity]
    internal Task<List<string>> SimpleMethodAsync(string someParam)
    {
        return Task.FromResult(new List<string>() { "foo:" + someParam, "bar:" + someParam });
    }

    [Activity]
    internal Task SimpleVoidMethodAsync(string someSuffix)
    {
        instanceState3 += someSuffix;
        return Task.CompletedTask;
    }

    internal async Task ExecuteActivityAsync(
        Action activity)
    {
        await ExecuteActivityInternalAsync<ValueTuple>(activity, null);
    }

    internal async Task ExecuteActivityAsync<T>(
        Action<T> activity, T arg)
    {
        await ExecuteActivityInternalAsync<ValueTuple>(activity, arg);
    }

    internal async Task ExecuteActivityAsync(
        Func<Task> activity,
        Func<WorkflowHandle, Task>? afterStarted = null,
        bool waitForCancellation = false,
        TimeSpan? heartbeatTimeout = null,
        int? maxAttempts = null,
        CancellationToken workerStoppingToken = default)
    {
        await ExecuteActivityInternalAsync<ValueTuple>(
            activity,
            afterStarted: afterStarted,
            workerStoppingToken: workerStoppingToken,
            waitForCancellation: waitForCancellation,
            heartbeatTimeout: heartbeatTimeout,
            maxAttempts: maxAttempts);
    }

    internal async Task ExecuteActivityAsync<T>(
        Func<T, Task> activity, T arg)
    {
        await ExecuteActivityInternalAsync<ValueTuple>(activity, arg);
    }

    internal Task<TResult> ExecuteActivityAsync<TResult>(
        Func<TResult> activity)
    {
        return ExecuteActivityInternalAsync<TResult>(activity, null);
    }

    internal Task<TResult> ExecuteActivityAsync<T, TResult>(
        Func<T, TResult> activity, T arg)
    {
        return ExecuteActivityInternalAsync<TResult>(activity, arg);
    }

    internal Task<TResult> ExecuteActivityAsync<TResult>(
        Func<Task<TResult>> activity,
        Func<WorkflowHandle, Task>? afterStarted = null,
        bool waitForCancellation = false,
        TimeSpan? heartbeatTimeout = null,
        CancellationToken workerStoppingToken = default)
    {
        return ExecuteActivityInternalAsync<TResult>(
            activity,
            afterStarted: afterStarted,
            workerStoppingToken: workerStoppingToken,
            waitForCancellation: waitForCancellation,
            heartbeatTimeout: heartbeatTimeout);
    }

    internal Task<TResult> ExecuteActivityAsync<T, TResult>(
        Func<T, Task<TResult>> activity, T arg)
    {
        return ExecuteActivityInternalAsync<TResult>(activity, arg);
    }

    internal Task<TResult> ExecuteActivityAsync<TResult>(
        Delegate activity,
        object? arg = null,
        object?[]? args = null)
    {
        return ExecuteActivityInternalAsync<TResult>(activity, arg, args);
    }

    internal async Task<TResult> ExecuteActivityInternalAsync<TResult>(
        Delegate activity,
        object? arg = null,
        object?[]? args = null,
        Func<WorkflowHandle, Task>? afterStarted = null,
        bool waitForCancellation = false,
        TimeSpan? heartbeatTimeout = null,
        int? maxAttempts = null,
        CancellationToken workerStoppingToken = default)
    {
        args ??= new object?[] { arg };
        // Run within worker
        var taskQueue = $"tq-{Guid.NewGuid()}";
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { activity },
        });
        return await worker.ExecuteAsync(
            async () =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    IKitchenSinkWorkflow.Ref.RunAsync,
                    new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                        Name: ActivityDefinition.FromDelegate(activity).Name,
                        TaskQueue: taskQueue,
                        Args: args,
                        WaitForCancellation: waitForCancellation,
                        HeartbeatTimeoutMS: (long?)heartbeatTimeout?.TotalMilliseconds,
                        RetryMaxAttempts: maxAttempts))),
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: Env.KitchenSinkWorkerTaskQueue));
                if (afterStarted != null)
                {
                    await afterStarted.Invoke(handle);
                }
                return await handle.GetResultAsync<TResult>();
            },
            workerStoppingToken);
    }
}