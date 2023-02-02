namespace Temporalio.Tests.Worker;

using Temporalio.Activity;
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
            [Activity("SimpleLambda")]
            (string param) => new List<string> { $"foo:{param}", $"bar:{param}" };
        Assert.Equal(
            new List<string>() { "foo:param", "bar:param" },
            await ExecuteActivityAsync(activity, "param"));
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
        Assert.Equal("InvalidOperationException", appErr.Type);
    }

    [Fact]
    public async Task ExecuteActivityAsync_ThrowApplicationFailureException_ReportsFailure()
    {
        [Activity]
        static void Throw() => throw new ApplicationFailureException(
            "Some message", type: "SomeType", details: new string[] { "foo", "bar" });
        var wfErr = await Assert.ThrowsAsync<WorkflowFailedException>(
            async () => await ExecuteActivityAsync(Throw));
        var actErr = Assert.IsType<ActivityFailureException>(wfErr.InnerException);
        var appErr = Assert.IsType<ApplicationFailureException>(actErr.InnerException);
        Assert.Equal("Some message", appErr.Message);
        Assert.Equal("SomeType", appErr.Type);
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
        var workerStoppingSource = new CancellationTokenSource();
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
        // Just to confirm there's not an issue with this exception
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
        await new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { Ignored },
        }).ExecuteAsync(async () =>
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
        await new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { WaitUntilCancelledAsync },
            MaxConcurrentActivities = 5,
        }).ExecuteAsync(async () =>
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
        var cancelReason = ActivityCancelReason.Unknown;
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
        await new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { CompleteExternal },
        }).ExecuteAsync(async () =>
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
        await new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { CompleteExternal },
        }).ExecuteAsync(async () =>
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
            Assert.Equal("InvalidOperationException", appErr.Type);
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
        await new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { CompleteExternal },
        }).ExecuteAsync(async () =>
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
    public void ExecuteAsync_PollFailure_ShutsDownWorker()
    {
    }

    [Fact]
    public void New_DuplicateActivityNames_Throws()
    {
        // TODO(cretz): Activity test framework too
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
        CancellationToken workerStoppingToken = default)
    {
        await ExecuteActivityInternalAsync<ValueTuple>(
            activity,
            afterStarted: afterStarted,
            workerStoppingToken: workerStoppingToken,
            waitForCancellation: waitForCancellation,
            heartbeatTimeout: heartbeatTimeout);
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

    internal Task<TResult> ExecuteActivityInternalAsync<TResult>(
        Delegate activity,
        object? arg = null,
        object?[]? args = null,
        Func<WorkflowHandle, Task>? afterStarted = null,
        bool waitForCancellation = false,
        TimeSpan? heartbeatTimeout = null,
        CancellationToken workerStoppingToken = default)
    {
        args ??= new object?[] { arg };
        // Run within worker
        var taskQueue = $"tq-{Guid.NewGuid()}";
        return new TemporalWorker(Client, new()
        {
            TaskQueue = taskQueue,
            Activities = { activity },
        }).ExecuteAsync(
            async () =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    IKitchenSinkWorkflow.Ref.RunAsync,
                    new KSWorkflowParams(new KSAction(ExecuteActivity: new(
                        Name: ActivityAttribute.Definition.FromDelegate(activity).Name,
                        TaskQueue: taskQueue,
                        Args: args,
                        WaitForCancellation: waitForCancellation,
                        HeartbeatTimeoutMS: (long?)heartbeatTimeout?.TotalMilliseconds))),
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