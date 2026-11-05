namespace Temporalio.Tests.Worker;

using System.Collections.Concurrent;
using Temporalio.Activities;
using Temporalio.Api.Common.V1;
using Temporalio.Api.History.V1;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Converters;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

// These tests pin the storage target attached at each outbound offload site. Round-trip offload
// and retrieval are covered by ExternalStorageEndToEndTests; here the driver records the target it
// was given per payload so each site's target kind and fields can be asserted against the spec.
public class ExternalStorageTargetTests : WorkflowEnvironmentTestBase
{
    public ExternalStorageTargetTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class TargetResultWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string input) => $"result: {input}";
    }

    [Workflow]
    public class TargetActivityWorkflow
    {
        [Activity]
        public static string Echo(string input) => $"activity: {input}";

        [WorkflowRun]
        public async Task<string> RunAsync(string input) =>
            await Workflow.ExecuteActivityAsync(
                () => Echo(input),
                new() { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
    }

    [Workflow]
    public class TargetSignalWorkflow
    {
        private string? received;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            await Workflow.WaitConditionAsync(() => received != null);
            return received!;
        }

        [WorkflowSignal]
        public async Task SubmitAsync(string value) => received = value;
    }

    [Activity]
    public static Task<string> StandaloneEcho(string input) => Task.FromResult($"standalone: {input}");

    [Workflow]
    public class ParkWorkflow
    {
        internal const string ActivityId = "park-act";

        [WorkflowRun]
        public async Task<string> RunAsync(string input) =>
            await Workflow.ExecuteActivityAsync<string>(
                "Park",
                new object?[] { input },
                new() { StartToCloseTimeout = TimeSpan.FromMinutes(1), ActivityId = ActivityId });
    }

    [Fact]
    public async Task ClientStartInput_TargetsWorkflowWithTypeButNoRun()
    {
        var (client, driver) = ClientWithCapture();

        // No worker polls this queue, so the client is the only possible offloader of the input.
        var workflowId = $"workflow-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (TargetResultWorkflow wf) => wf.RunAsync("hi"),
            new(id: workflowId, taskQueue: $"tq-{Guid.NewGuid()}"));

        try
        {
            var started = await FirstEventAsync(
                handle, e => e.WorkflowExecutionStartedEventAttributes != null);
            var target = Assert.IsType<IStorageDriverTargetInfo.Workflow>(
                driver.TargetFor(started.WorkflowExecutionStartedEventAttributes.Input));
            Assert.Equal(client.Options.Namespace, target.Namespace);
            Assert.Equal(workflowId, target.WorkflowId);
            Assert.Null(target.RunId);
            Assert.Equal("TargetResultWorkflow", target.WorkflowType);
        }
        finally
        {
            await handle.TerminateAsync();
        }
    }

    [Fact]
    public async Task ClientStartMemo_TargetsWorkflowWithTypeButNoRun()
    {
        var (client, driver) = ClientWithCapture();

        var workflowId = $"workflow-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (TargetResultWorkflow wf) => wf.RunAsync("hi"),
            new(id: workflowId, taskQueue: $"tq-{Guid.NewGuid()}")
            {
                Memo = new Dictionary<string, object> { ["key"] = "memo-value" },
            });

        try
        {
            var started = await FirstEventAsync(
                handle, e => e.WorkflowExecutionStartedEventAttributes != null);
            var target = Assert.IsType<IStorageDriverTargetInfo.Workflow>(
                driver.TargetFor(started.WorkflowExecutionStartedEventAttributes.Memo.Fields["key"]));
            Assert.Equal(client.Options.Namespace, target.Namespace);
            Assert.Equal(workflowId, target.WorkflowId);
            Assert.Null(target.RunId);
            Assert.Equal("TargetResultWorkflow", target.WorkflowType);
        }
        finally
        {
            await handle.TerminateAsync();
        }
    }

    [Fact]
    public async Task ClientSignalInput_TargetsWorkflowWithRunButNoType()
    {
        var (client, driver) = ClientWithCapture();

        var workflowId = $"workflow-{Guid.NewGuid()}";
        var handle = await client.StartWorkflowAsync(
            (TargetSignalWorkflow wf) => wf.RunAsync(),
            new(id: workflowId, taskQueue: $"tq-{Guid.NewGuid()}"));

        try
        {
            await handle.SignalAsync(wf => wf.SubmitAsync("signaled"));

            var signaled = await FirstEventAsync(
                handle, e => e.WorkflowExecutionSignaledEventAttributes != null);
            var target = Assert.IsType<IStorageDriverTargetInfo.Workflow>(
                driver.TargetFor(signaled.WorkflowExecutionSignaledEventAttributes.Input));
            Assert.Equal(client.Options.Namespace, target.Namespace);
            Assert.Equal(workflowId, target.WorkflowId);
            Assert.Equal(handle.RunId, target.RunId);
            Assert.Null(target.WorkflowType);
        }
        finally
        {
            await handle.TerminateAsync();
        }
    }

    [Fact]
    public async Task WorkerActivityResult_TargetsOwningWorkflowWithAllFields()
    {
        var (client, driver) = ClientWithCapture();
        await ExecuteWorkerAsync<TargetActivityWorkflow>(
            client,
            async worker =>
            {
                var handle = await client.StartWorkflowAsync(
                    (TargetActivityWorkflow wf) => wf.RunAsync("hi"), NewOptions(worker));
                Assert.Equal("activity: hi", await handle.GetResultAsync());

                // The activity result is offloaded by the activity worker against the owning
                // workflow, so the target carries all four workflow fields.
                var completed = await FirstEventAsync(
                    handle, e => e.ActivityTaskCompletedEventAttributes != null);
                var target = Assert.IsType<IStorageDriverTargetInfo.Workflow>(
                    driver.TargetFor(completed.ActivityTaskCompletedEventAttributes.Result));
                Assert.Equal(client.Options.Namespace, target.Namespace);
                Assert.Equal(handle.Id, target.WorkflowId);
                Assert.Equal(handle.ResultRunId, target.RunId);
                Assert.Equal("TargetActivityWorkflow", target.WorkflowType);

                // The activity arg rides the whole-workflow-task completion, which has no
                // per-command target yet, so it is offloaded without one.
                var scheduled = await FirstEventAsync(
                    handle, e => e.ActivityTaskScheduledEventAttributes != null);
                Assert.Null(driver.TargetFor(scheduled.ActivityTaskScheduledEventAttributes.Input));
            },
            new TemporalWorkerOptions().AddActivity(TargetActivityWorkflow.Echo));
    }

    [Fact]
    public async Task StandaloneActivityResult_TargetsActivityWithAllFields()
    {
        var (client, driver) = ClientWithCapture();
        var taskQueue = $"tq-{Guid.NewGuid()}";
        var activityId = $"act-{Guid.NewGuid()}";
        var options = new TemporalWorkerOptions(taskQueue).AddActivity(StandaloneEcho);
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(async () =>
        {
            var result = await client.ExecuteActivityAsync(
                () => StandaloneEcho("hi"),
                new(activityId, taskQueue)
                {
                    ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
                });
            Assert.Equal("standalone: hi", result);
        });

        // The worker offloads the result against the standalone activity itself, with the run id
        // threaded from the task start. (The client-side arg offload does not yet know the run id.)
        Assert.Contains(
            driver.RecordedTargets,
            t => t is IStorageDriverTargetInfo.Activity activity &&
                activity.Namespace == client.Options.Namespace &&
                activity.ActivityId == activityId &&
                activity.ActivityType == "StandaloneEcho" &&
                !string.IsNullOrEmpty(activity.RunId));
    }

    [Fact]
    public async Task AsyncActivityCompleteById_TargetsSchedulingWorkflow()
    {
        var (client, driver) = ClientWithCapture();
        await RunParkedActivityAsync(client, async (reference, completeWith) =>
        {
            var before = driver.RecordedTargets.Length;
            await client.GetAsyncActivityHandle(
                reference.WorkflowId, reference.RunId, reference.ActivityId)
                .CompleteAsync(completeWith);

            // The id reference names the scheduling workflow, so the result is offloaded against it
            // (with no workflow type, which the reference does not carry).
            var completionTarget = Assert.Single(driver.RecordedTargets.Skip(before));
            var target = Assert.IsType<IStorageDriverTargetInfo.Workflow>(completionTarget);
            Assert.Equal(client.Options.Namespace, target.Namespace);
            Assert.Equal(reference.WorkflowId, target.WorkflowId);
            Assert.Equal(reference.RunId, target.RunId);
            Assert.Null(target.WorkflowType);
        });
    }

    [Fact]
    public async Task AsyncActivityCompleteByTaskToken_HasNoTarget()
    {
        var (client, driver) = ClientWithCapture();
        await RunParkedActivityAsync(client, async (reference, completeWith) =>
        {
            var before = driver.RecordedTargets.Length;
            await client.GetAsyncActivityHandle(reference.TaskToken).CompleteAsync(completeWith);

            // A task token carries no execution identity, so the result is offloaded without a
            // target.
            Assert.Null(Assert.Single(driver.RecordedTargets.Skip(before)));
        });
    }

    [Fact]
    public async Task ScheduleAction_TargetsStartedWorkflowButOwnMemoHasNoTarget()
    {
        var (client, driver) = ClientWithCapture();
        var scheduleId = $"schedule-{Guid.NewGuid()}";
        var workflowId = $"workflow-{Guid.NewGuid()}";
        var action = ScheduleActionStartWorkflow.Create(
            "TargetResultWorkflow",
            new object?[] { "hi" },
            new(id: workflowId, taskQueue: $"tq-{Guid.NewGuid()}"));
        var schedule = new Schedule(
            Action: action,
            Spec: new() { Calendars = new List<ScheduleCalendarSpec> { new() } })
        {
            State = new() { Paused = true },
        };
        var scheduleOptions = new ScheduleOptions
        {
            Memo = new Dictionary<string, object> { ["scheduleMemo"] = "own" },
        };

        var handle = await client.CreateScheduleAsync(scheduleId, schedule, scheduleOptions);
        try
        {
            // The action's payloads name the workflow the schedule will start.
            Assert.Contains(
                driver.RecordedTargets,
                t => t is IStorageDriverTargetInfo.Workflow w &&
                    w.Namespace == client.Options.Namespace &&
                    w.WorkflowId == workflowId &&
                    w.RunId == null &&
                    w.WorkflowType == "TargetResultWorkflow");

            // The schedule's own memo is read by the internal scheduler workflow, which the SDK
            // cannot name, so it is offloaded without a target. It is the only untargeted offload
            // here, since every action payload names the started workflow.
            Assert.Contains(driver.RecordedTargets, t => t == null);
        }
        finally
        {
            await handle.DeleteAsync();
        }
    }

    private static WorkflowOptions NewOptions(TemporalWorker worker) =>
        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!);

    private static async Task<HistoryEvent> FirstEventAsync(
        WorkflowHandle handle, Func<HistoryEvent, bool> predicate)
    {
        await foreach (var evt in handle.FetchHistoryEventsAsync())
        {
            if (predicate(evt))
            {
                return evt;
            }
        }

        throw new InvalidOperationException("No matching history event found.");
    }

    private (TemporalClient Client, TargetCapturingDriver Driver) ClientWithCapture()
    {
        var driver = new TargetCapturingDriver();
        var options = (TemporalClientOptions)Client.Options.Clone();
        options.DataConverter = DataConverter.Default with
        {
            ExternalStorage = new ExternalStorage(driver) { PayloadSizeThreshold = 1 },
        };
        return (new TemporalClient(Client.Connection, options), driver);
    }

    private async Task ExecuteWorkerAsync<TWorkflow>(
        ITemporalClient client,
        Func<TemporalWorker, Task> action,
        TemporalWorkerOptions? options = null)
    {
        options ??= new();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow<TWorkflow>();
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    // Runs a workflow whose activity parks for async completion, then hands the complete callback
    // the activity's identity plus the result string to complete it with, and asserts the result
    // round-trips back as the workflow result.
    private async Task RunParkedActivityAsync(
        TemporalClient client,
        Func<(string WorkflowId, string RunId, string ActivityId, byte[] TaskToken), string, Task>
            complete)
    {
        const string result = "completed: hi";
        var reached =
            new TaskCompletionSource<(string, string, string, byte[])>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        [Activity("Park")]
        string Park(string input)
        {
            var info = ActivityExecutionContext.Current.Info;
            reached.SetResult(
                (info.WorkflowId!, info.WorkflowRunId!, info.ActivityId, info.TaskToken));
            throw new CompleteAsyncException();
        }

        var taskQueue = $"tq-{Guid.NewGuid()}";
        var options = new TemporalWorkerOptions(taskQueue)
            .AddWorkflow<ParkWorkflow>()
            .AddActivity(Park);
        options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(async () =>
        {
            var handle = await client.StartWorkflowAsync(
                (ParkWorkflow wf) => wf.RunAsync("hi"),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: taskQueue));
            var reference = await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await complete(reference, result);
            Assert.Equal(result, await handle.GetResultAsync());
        });
    }

    private sealed class TargetCapturingDriver : IStorageDriver
    {
        // Worker and client reach this concurrently, so all collections are thread-safe. Targets
        // are recorded both per claim key (to map a reference read from history back to its target)
        // and in store order (to inspect what a specific call, e.g. an async completion, offloaded).
        private readonly ConcurrentDictionary<string, Payload> stored = new();
        private readonly ConcurrentDictionary<string, IStorageDriverTargetInfo?> targets = new();
        private readonly ConcurrentQueue<IStorageDriverTargetInfo?> recorded = new();

        public string Name => "mem";

        public string Type => "test.inmemory";

        public IStorageDriverTargetInfo?[] RecordedTargets => recorded.ToArray();

        public Task<IReadOnlyCollection<StorageDriverClaim>> StoreAsync(
            StorageDriverStoreContext context,
            IReadOnlyCollection<Payload> payloads,
            CancellationToken cancellationToken)
        {
            var claims = new List<StorageDriverClaim>();
            foreach (var payload in payloads)
            {
                var key = Guid.NewGuid().ToString();
                stored[key] = payload;
                targets[key] = context.Target;
                recorded.Enqueue(context.Target);
                claims.Add(new StorageDriverClaim(new Dictionary<string, string> { ["key"] = key }));
            }

            return Task.FromResult<IReadOnlyCollection<StorageDriverClaim>>(claims);
        }

        public Task<IReadOnlyCollection<Payload>> RetrieveAsync(
            StorageDriverRetrieveContext context,
            IReadOnlyCollection<StorageDriverClaim> claims,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Payload>>(
                claims.Select(c => stored[c.ClaimData["key"]]).ToList());

        // The target recorded when the given reference payload was offloaded.
        public IStorageDriverTargetInfo? TargetFor(Payload reference) =>
            TargetFor(ExternalStorageReferences.ParseReference(reference));

        // The target recorded for the single reference inside the given payloads.
        public IStorageDriverTargetInfo? TargetFor(Payloads references) =>
            TargetFor(Assert.Single(references.Payloads_));

        private IStorageDriverTargetInfo? TargetFor(Api.Sdk.V1.ExternalStorageReference reference) =>
            targets[reference.ClaimData["key"]];
    }
}
