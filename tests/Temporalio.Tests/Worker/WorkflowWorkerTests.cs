#pragma warning disable CA1724 // Don't care about name conflicts
#pragma warning disable CS1998 // Sometimes I wait "async" functions with no await in them
#pragma warning disable SA1201, SA1204 // We want to have classes near their tests

namespace Temporalio.Tests.Worker;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Exceptions;
using Temporalio.Worker;
using Temporalio.Workflows;
using Xunit;
using Xunit.Abstractions;

public class WorkflowWorkerTests : WorkflowEnvironmentTestBase
{
    public WorkflowWorkerTests(ITestOutputHelper output, WorkflowEnvironment env)
        : base(output, env)
    {
    }

    [Workflow]
    public class SimpleWorkflow
    {
        public static readonly SimpleWorkflow Ref = Refs.Create<SimpleWorkflow>();

        [WorkflowRun]
        public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Simple_Succeeds()
    {
        await ExecuteWorkerAsync<SimpleWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                SimpleWorkflow.Ref.RunAsync,
                "Temporal",
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_ManualDefinition_Succeeds()
    {
        using var worker = new TemporalWorker(Client, new()
        {
            TaskQueue = $"tq-{Guid.NewGuid()}",
            AdditionalWorkflowDefinitions =
            {
                WorkflowDefinition.CreateWithoutAttribute("other-name", typeof(SimpleWorkflow)),
            },
        });
        await worker.ExecuteAsync(async () =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync<string>(
                "other-name",
                new[] { "Temporal" },
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_UnknownWorkflow_ProperlyFails()
    {
        await ExecuteWorkerAsync<SimpleWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                "not-found-workflow",
                Array.Empty<object?>(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            await AssertTaskFailureContainsEventuallyAsync(handle, "not registered");
        });
    }

    [Workflow]
    public class WorkflowInitWorkflow
    {
        public static readonly WorkflowInitWorkflow Ref = Refs.Create<WorkflowInitWorkflow>();
        private readonly string name;

        [WorkflowInit]
        public WorkflowInitWorkflow(string name) => this.name = name;

        [WorkflowRun]
        public Task<string> RunAsync(string _) => Task.FromResult($"Hello, {name}!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WorkflowInit_Succeeds()
    {
        await ExecuteWorkerAsync<WorkflowInitWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                WorkflowInitWorkflow.Ref.RunAsync,
                "Temporal",
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class WorkflowInitNoParamsWorkflow
    {
        public static readonly WorkflowInitNoParamsWorkflow Ref = Refs.Create<WorkflowInitNoParamsWorkflow>();

        [WorkflowInit]
        public WorkflowInitNoParamsWorkflow()
        {
        }

        [WorkflowRun]
        public Task<string> RunAsync() => Task.FromResult($"Hello, Temporal!");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WorkflowInitNoParams_Succeeds()
    {
        await ExecuteWorkerAsync<WorkflowInitNoParamsWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync(
                WorkflowInitNoParamsWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("Hello, Temporal!", result);
        });
    }

    [Workflow]
    public class InvalidTaskCallsWorkflow
    {
        public static readonly InvalidTaskCallsWorkflow Ref = Refs.Create<InvalidTaskCallsWorkflow>();

        [WorkflowRun]
        public async Task RunAsync(string scenario)
        {
            switch (scenario)
            {
                case "Task.Delay":
                    await Task.Delay(5000);
                    break;
                default:
                    throw new NotImplementedException(scenario);
            }
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_InvalidTaskCalls_FailsTask()
    {
        async Task AssertScenarioFailsTask(string scenario, string exceptionContains)
        {
            await ExecuteWorkerAsync<InvalidTaskCallsWorkflow>(async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    InvalidTaskCallsWorkflow.Ref.RunAsync,
                    scenario,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));

                // Make sure task failure occurs in history eventually
                await AssertTaskFailureContainsEventuallyAsync(handle, exceptionContains);
            });
        }

        await AssertScenarioFailsTask("Task.Delay", "Task.Delay cannot be used in workflows");
        // TODO(cretz): Scenarios to prevent:
        // * Task run/create/etc on different scheduler
        //   * Note Task.Run uses default scheduler by default so we should error here
        //   * Note, ConfigureAwait(false) uses default scheduler too!
        // * Task wait with timeout
        // * CancellationTokenSource.CancelAfter (and all TimerQueueTimer uses)
        // * ThreadPool.QueueUserWorkIterm
        // * Timers - https://learn.microsoft.com/en-us/dotnet/standard/threading/timers
        // * IO
    }

    [Workflow]
    public class InfoWorkflow
    {
        public static readonly InfoWorkflow Ref = Refs.Create<InfoWorkflow>();

        [WorkflowRun]
        public Task<WorkflowInfo> RunAsync() => Task.FromResult(Workflow.Info);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Info_Succeeds()
    {
        await ExecuteWorkerAsync<InfoWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                InfoWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            var result = await handle.GetResultAsync();
            Assert.Equal(1, result.Attempt);
            Assert.Null(result.ContinuedRunID);
            Assert.Null(result.CronSchedule);
            Assert.Null(result.ExecutionTimeout);
            Assert.Equal(worker.Client.Options.Namespace, result.Namespace);
            Assert.Null(result.Parent);
            Assert.Null(result.RetryPolicy);
            Assert.Equal(handle.ResultRunID, result.RunID);
            Assert.Null(result.RunTimeout);
            Assert.InRange(
                result.StartTime,
                DateTime.UtcNow - TimeSpan.FromMinutes(5),
                DateTime.UtcNow + TimeSpan.FromMinutes(5));
            Assert.Equal(worker.Options.TaskQueue, result.TaskQueue);
            // TODO(cretz): Can assume default 10 in all test servers?
            Assert.Equal(TimeSpan.FromSeconds(10), result.TaskTimeout);
            Assert.Equal(handle.ID, result.WorkflowID);
            Assert.Equal("InfoWorkflow", result.WorkflowType);
        });
    }

    [Workflow]
    public class MultiParamWorkflow
    {
        public static readonly MultiParamWorkflow Ref = Refs.Create<MultiParamWorkflow>();

        [WorkflowRun]
        public Task<string> RunAsync(string param1, string param2) => Task.FromResult($"{param1}:{param2}");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_MultiParam_Succeeds()
    {
        await ExecuteWorkerAsync<MultiParamWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync<string>(
                "MultiParamWorkflow",
                new string[] { "foo", "bar" },
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("foo:bar", result);
        });
    }

    [Workflow]
    public class DefaultParamWorkflow
    {
        public static readonly DefaultParamWorkflow Ref = Refs.Create<DefaultParamWorkflow>();

        [WorkflowRun]
        public Task<string> RunAsync(string param1, string param2 = "default") =>
            Task.FromResult($"{param1}:{param2}");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_DefaultParam_Succeeds()
    {
        await ExecuteWorkerAsync<DefaultParamWorkflow>(async worker =>
        {
            var result = await Env.Client.ExecuteWorkflowAsync<string>(
                "DefaultParamWorkflow",
                new string[] { "foo" },
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("foo:default", result);
        });
    }

    [Workflow]
    public class TimerWorkflow
    {
        public static readonly TimerWorkflow Ref = Refs.Create<TimerWorkflow>();

        [WorkflowRun]
        public async Task<string> RunAsync(Input input)
        {
            using var cancelSource = new CancellationTokenSource();
            if (input.CancelBefore)
            {
                cancelSource.Cancel();
            }
            else if (input.CancelAfterMS > 0)
            {
                _ = Workflow.DelayAsync(input.CancelAfterMS).ContinueWith(_ => cancelSource.Cancel());
            }
            try
            {
                await Workflow.DelayAsync(input.DelayMS, cancelSource.Token);
                return "done";
            }
            catch (TaskCanceledException)
            {
                return "cancelled";
            }
        }

        public record Input(
            int DelayMS,
            bool CancelBefore = false,
            int CancelAfterMS = 0);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Timer_Succeeds()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 100),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("done", await handle.GetResultAsync());
            // Check history has a timer start and a timer fire
            bool foundStart = false, foundFire = false;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.TimerStartedEventAttributes != null)
                {
                    Assert.False(foundStart);
                    foundStart = true;
                    Assert.Equal(
                        TimeSpan.FromMilliseconds(100),
                        evt.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan());
                }
                else if (evt.TimerFiredEventAttributes != null)
                {
                    Assert.False(foundFire);
                    foundFire = true;
                }
            }
            Assert.True(foundStart);
            Assert.True(foundFire);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerCancelBefore_ProperlyCancelled()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 10000, CancelBefore: true),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("cancelled", await handle.GetResultAsync());
            // Make sure no timer event in history
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                Assert.Null(evt.TimerStartedEventAttributes);
            }
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerCancelAfter_ProperlyCancelled()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 10000, CancelAfterMS: 100),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("cancelled", await handle.GetResultAsync());
            // Make sure there is a timer cancelled in history
            var found = false;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                found = evt.TimerCanceledEventAttributes != null;
                if (found)
                {
                    break;
                }
            }
            Assert.True(found);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerInfinite_NeverCreatesTask()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: Timeout.Infinite, CancelAfterMS: 100),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("cancelled", await handle.GetResultAsync());
            // Only expect the one cancel timer, not the infinite one
            var timerCount = 0;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.TimerStartedEventAttributes != null)
                {
                    timerCount++;
                }
            }
            Assert.Equal(1, timerCount);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerNegative_FailsTask()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: -50),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Confirm WFT failure
            await AssertTaskFailureContainsEventuallyAsync(handle, "duration cannot be less than 0");
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimerZero_LikeOneMillisecond()
    {
        await ExecuteWorkerAsync<TimerWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                TimerWorkflow.Ref.RunAsync,
                new TimerWorkflow.Input(DelayMS: 0),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            Assert.Equal("done", await handle.GetResultAsync());
            // Make sure the timer was set to 1ms
            var found = false;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.TimerStartedEventAttributes != null)
                {
                    found = true;
                    Assert.Equal(
                        TimeSpan.FromMilliseconds(1),
                        evt.TimerStartedEventAttributes.StartToFireTimeout.ToTimeSpan());
                }
            }
            Assert.True(found);
        });
    }

    [Workflow]
    public class CancelWorkflow
    {
        public static readonly CancelWorkflow Ref = Refs.Create<CancelWorkflow>();

        public static TaskCompletionSource? ActivityStarted { get; set; }

        [Activity]
        public static async Task<string> SwallowCancelActivityAsync()
        {
            ActivityStarted!.SetResult();
            while (!ActivityContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityContext.Current.Heartbeat();
                await Task.Delay(100);
            }
            return "cancelled";
        }

        [Workflow]
        public class SwallowCancelChildWorkflow
        {
            public static readonly SwallowCancelChildWorkflow Ref = Refs.Create<SwallowCancelChildWorkflow>();

            [WorkflowRun]
            public async Task<string> RunAsync()
            {
                try
                {
                    await Workflow.DelayAsync(Timeout.Infinite);
                    throw new InvalidOperationException("Unexpected timer success");
                }
                catch (OperationCanceledException)
                {
                    return "cancelled";
                }
            }
        }

        [WorkflowRun]
        public async Task<string> RunAsync(Scenario scenario)
        {
            switch (scenario)
            {
                case Scenario.Timer:
                    await Workflow.DelayAsync(30000);
                    break;
                case Scenario.TimerIgnoreCancel:
                    // Wait for both cancellation and timer
                    var timerTask = Workflow.DelayAsync(100, CancellationToken.None);
                    try
                    {
                        await timerTask.WaitAsync(Workflow.CancellationToken);
                        Assert.Fail("Didn't fail");
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    await timerTask;
                    break;
                case Scenario.TimerWaitAndIgnore:
                    await Task.WhenAny(Workflow.DelayAsync(Timeout.Infinite, Workflow.CancellationToken));
                    break;
                case Scenario.Activity:
                    await Workflow.ExecuteActivityAsync(
                        SwallowCancelActivityAsync,
                        new()
                        {
                            ScheduleToCloseTimeout = TimeSpan.FromHours(1),
                            HeartbeatTimeout = TimeSpan.FromSeconds(2),
                        });
                    break;
                case Scenario.ActivityWaitAndIgnore:
                    var actRes = await Workflow.ExecuteActivityAsync(
                        SwallowCancelActivityAsync,
                        new()
                        {
                            ScheduleToCloseTimeout = TimeSpan.FromHours(1),
                            HeartbeatTimeout = TimeSpan.FromSeconds(2),
                            CancellationType = ActivityCancellationType.WaitCancellationCompleted,
                        });
                    Assert.Equal("cancelled", actRes);
                    break;
                case Scenario.LocalActivity:
                    await Workflow.ExecuteLocalActivityAsync(
                        SwallowCancelActivityAsync,
                        new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                    break;
                case Scenario.ChildTryCancel:
                    await Workflow.ExecuteChildWorkflowAsync(
                        SwallowCancelChildWorkflow.Ref.RunAsync,
                        new() { CancellationType = ChildWorkflowCancellationType.TryCancel });
                    break;
                case Scenario.ChildWaitAndIgnore:
                    var childRes = await Workflow.ExecuteChildWorkflowAsync(
                        SwallowCancelChildWorkflow.Ref.RunAsync);
                    Assert.Equal("cancelled", childRes);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return "done";
        }

        public enum Scenario
        {
            Timer,
            TimerIgnoreCancel,
            TimerWaitAndIgnore,
            Activity,
            ActivityWaitAndIgnore,
            LocalActivity,
            ChildTryCancel,
            ChildWaitAndIgnore,
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Cancel_ProperlyCancelled()
    {
        Task AssertProperlyCancelled(
            CancelWorkflow.Scenario scenario,
            Func<WorkflowHandle, Task>? waitFor = null,
            Action<WorkflowHandle>? additionalAssertions = null) =>
            ExecuteWorkerAsync<CancelWorkflow>(
                async worker =>
                {
                    CancelWorkflow.ActivityStarted = new();
                    var handle = await Env.Client.StartWorkflowAsync(
                        CancelWorkflow.Ref.RunAsync,
                        scenario,
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                        {
                            // Lowering for quicker LA result
                            TaskTimeout = TimeSpan.FromSeconds(2),
                        });
                    if (waitFor != null)
                    {
                        await waitFor(handle);
                    }
                    else
                    {
                        await AssertStartedEventuallyAsync(handle);
                    }
                    await handle.CancelAsync();
                    var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                        () => handle.GetResultAsync());
                    Assert.IsType<CancelledFailureException>(exc.InnerException);
                    additionalAssertions?.Invoke(handle);
                },
                new()
                {
                    Activities = { CancelWorkflow.SwallowCancelActivityAsync },
                    Workflows = { typeof(CancelWorkflow.SwallowCancelChildWorkflow) },
                });

        // TODO(cretz): wait condition, external signal, etc
        await AssertProperlyCancelled(CancelWorkflow.Scenario.Timer);
        await AssertProperlyCancelled(
            CancelWorkflow.Scenario.Activity, _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyCancelled(
            CancelWorkflow.Scenario.LocalActivity, _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyCancelled(
            CancelWorkflow.Scenario.ChildTryCancel, AssertChildInitiatedEventuallyAsync);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Cancel_ProperlyIgnored()
    {
        Task AssertProperlyIgnored(
            CancelWorkflow.Scenario scenario,
            Func<WorkflowHandle, Task>? waitFor = null,
            Action<WorkflowHandle>? additionalAssertions = null) =>
            ExecuteWorkerAsync<CancelWorkflow>(
                async worker =>
                {
                    CancelWorkflow.ActivityStarted = new();
                    var handle = await Env.Client.StartWorkflowAsync(
                        CancelWorkflow.Ref.RunAsync,
                        scenario,
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                    if (waitFor != null)
                    {
                        await waitFor(handle);
                    }
                    else
                    {
                        await AssertStartedEventuallyAsync(handle);
                    }
                    await handle.CancelAsync();
                    Assert.Equal("done", await handle.GetResultAsync());
                    additionalAssertions?.Invoke(handle);
                },
                new()
                {
                    Activities = { CancelWorkflow.SwallowCancelActivityAsync },
                    Workflows = { typeof(CancelWorkflow.SwallowCancelChildWorkflow) },
                });

        // TODO(cretz): Test external signal, etc
        await AssertProperlyIgnored(CancelWorkflow.Scenario.TimerIgnoreCancel);
        await AssertProperlyIgnored(CancelWorkflow.Scenario.TimerWaitAndIgnore);
        await AssertProperlyIgnored(
            CancelWorkflow.Scenario.ActivityWaitAndIgnore,
            _ => CancelWorkflow.ActivityStarted!.Task);
        await AssertProperlyIgnored(
            CancelWorkflow.Scenario.ChildWaitAndIgnore, AssertChildInitiatedEventuallyAsync);
    }

    [Workflow]
    public class AssertWorkflow
    {
        public static readonly AssertWorkflow Ref = Refs.Create<AssertWorkflow>();

        [WorkflowRun]
        public async Task RunAsync() => Assert.Fail("Oh no");
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Assert_FailsWorkflow()
    {
        await ExecuteWorkerAsync<AssertWorkflow>(async worker =>
        {
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                Env.Client.ExecuteWorkflowAsync(
                    AssertWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
            var exc2 = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Equal("Assertion failed", exc2.Message);
            var exc3 = Assert.IsType<ApplicationFailureException>(exc2.InnerException);
            Assert.Contains("Oh no", exc3.Message);
        });
    }

    [Workflow]
    public class SignalWorkflow
    {
        public static readonly SignalWorkflow Ref = Refs.Create<SignalWorkflow>();
        private readonly List<string> events = new();

        // Just wait forever
        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

        [WorkflowSignal]
        public async Task Signal1Async(string arg) => events.Add($"Signal1: {arg}");

        [WorkflowSignal("custom-name")]
        public async Task SignalCustomAsync(string arg) => events.Add($"SignalCustom: {arg}");

        [WorkflowSignal]
        public async Task AddSignalHandlersAsync(IReadOnlyCollection<string> names)
        {
            foreach (var name in names)
            {
                async Task HandleSignalAsync(string arg) =>
                    events.Add($"AddSignalHandlers: {name} - {arg}");
                Workflow.Signals[name] = WorkflowSignalDefinition.CreateWithoutAttribute(
                    name, HandleSignalAsync);
            }
        }

        [WorkflowSignal]
        public async Task FailWorkflowAsync() => throw new ApplicationFailureException("Oh no");

        [WorkflowQuery]
        public IList<string> Events() => events;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Signals_ProperlyHandled()
    {
        await ExecuteWorkerAsync<SignalWorkflow>(async worker =>
        {
            // Start the workflow with a signal
            var handle = await Env.Client.StartWorkflowAsync(
                SignalWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    StartSignal = "Signal1",
                    StartSignalArgs = new[] { "signalval1" },
                });
            // Add one w/ custom name
            await handle.SignalAsync(SignalWorkflow.Ref.SignalCustomAsync, "signalval2");
            // Add a couple more to be buffered
            await handle.SignalAsync("latesig1", new[] { "signalval3" });
            await handle.SignalAsync("latesig2", new[] { "signalval4" });
            // Add the signal handlers
            await handle.SignalAsync(
                SignalWorkflow.Ref.AddSignalHandlersAsync,
                new[] { "latesig1", "latesig2", "latesig3" });
            // Add another to a late-bound signal
            await handle.SignalAsync("latesig3", new[] { "signalval5" });
            // Confirm all events are as expected
            Assert.Equal(
                new List<string>
                {
                    "Signal1: signalval1",
                    "SignalCustom: signalval2",
                    "AddSignalHandlers: latesig1 - signalval3",
                    "AddSignalHandlers: latesig2 - signalval4",
                    "AddSignalHandlers: latesig3 - signalval5",
                },
                await handle.QueryAsync(SignalWorkflow.Ref.Events));

            // Now send a signal that fails the entire workflow
            await handle.SignalAsync(SignalWorkflow.Ref.FailWorkflowAsync);
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            var exc2 = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Equal("Oh no", exc2.Message);
        });
    }

    [Workflow]
    public class QueryWorkflow
    {
        public static readonly QueryWorkflow Ref = Refs.Create<QueryWorkflow>();

        // Just wait forever
        [WorkflowRun]
        public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);

        [WorkflowQuery]
        public string QuerySimple(string arg) => $"QuerySimple: {arg}";

        [WorkflowQuery("custom-name")]
        public string QueryCustom(string arg) => $"QueryCustom: {arg}";

        [WorkflowSignal]
        public async Task AddQueryHandlerAsync(string name) =>
            Workflow.Queries[name] = WorkflowQueryDefinition.CreateWithoutAttribute(
                name, (string arg) => $"AddQueryHandler: {name} - {arg}");

        [WorkflowQuery]
        public string QueryFail() => throw new InvalidOperationException("Query fail");

        [WorkflowQuery]
        public string QueryMakingCommands()
        {
            _ = Workflow.DelayAsync(5000);
            return "unreachable";
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Queries_ProperlyHandled()
    {
        await ExecuteWorkerAsync<QueryWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                QueryWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Basic queries
            Assert.Equal(
                "QuerySimple: foo",
                await handle.QueryAsync(QueryWorkflow.Ref.QuerySimple, "foo"));
            Assert.Equal(
                "QueryCustom: bar",
                await handle.QueryAsync(QueryWorkflow.Ref.QueryCustom, "bar"));
            // Non-existent query
            var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync<string>("some-query", new[] { "some-arg" }));
            Assert.Contains(
                "known queries: [custom-name QueryFail QueryMakingCommands QuerySimple]",
                exc.Message);
            // Add that non-existent query then try again
            await handle.SignalAsync(QueryWorkflow.Ref.AddQueryHandlerAsync, "some-query");
            Assert.Equal(
                "AddQueryHandler: some-query - some-arg",
                await handle.QueryAsync<string>("some-query", new[] { "some-arg" }));
            // Query fail
            var exc2 = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync(QueryWorkflow.Ref.QueryFail));
            Assert.Equal("Query fail", exc2.Message);
            // Make commands in a query
            var exc3 = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                () => handle.QueryAsync(QueryWorkflow.Ref.QueryMakingCommands));
            Assert.Contains("created workflow commands", exc3.Message);
        });
    }

    [Workflow]
    public class MiscHelpersWorkflow
    {
        public static readonly MiscHelpersWorkflow Ref = Refs.Create<MiscHelpersWorkflow>();

        private static readonly List<bool> EventsForIsReplaying = new();

        private string? completeWorkflow;

        [WorkflowRun]
        public async Task<string> RunAsync()
        {
            Workflow.Logger.LogInformation("Some log {Foo}", "Bar");
            EventsForIsReplaying.Add(Workflow.Unsafe.IsReplaying);
            await Workflow.WaitConditionAsync(() => completeWorkflow != null);
            return completeWorkflow!;
        }

        [WorkflowSignal]
        public async Task CompleteWorkflow(string val) => completeWorkflow = val;

        [WorkflowQuery]
        public DateTime CurrentTime() => Workflow.UtcNow;

        [WorkflowQuery]
        public int Random(int max) => Workflow.Random.Next(max);

        [WorkflowQuery]
        public string NewGuid() => Workflow.NewGuid().ToString();

        [WorkflowQuery]
        public bool[] GetEventsForIsReplaying() => EventsForIsReplaying.ToArray();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_MiscHelpers_Succeed()
    {
        // Run one worker doing test
        var workflowID = string.Empty;
        var taskQueue = string.Empty;
        var loggerFactory = new TestUtils.LogCaptureFactory(LoggerFactory);
        await ExecuteWorkerAsync<MiscHelpersWorkflow>(
            async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    MiscHelpersWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                workflowID = handle.ID;
                taskQueue = worker.Options.TaskQueue!;
                Assert.InRange(
                    await handle.QueryAsync(MiscHelpersWorkflow.Ref.CurrentTime),
                    DateTime.UtcNow - TimeSpan.FromMinutes(5),
                    DateTime.UtcNow + TimeSpan.FromMinutes(5));
                Assert.InRange(
                    await handle.QueryAsync(MiscHelpersWorkflow.Ref.Random, 3), 0, 2);
                // Check GUID is parseable and shows 4 as UUID version
                var guid = await handle.QueryAsync(MiscHelpersWorkflow.Ref.NewGuid);
                Assert.True(Guid.TryParseExact(guid, "D", out _));
                Assert.Equal('4', guid[14]);
                // Mark workflow complete and wait on result
                await handle.SignalAsync(MiscHelpersWorkflow.Ref.CompleteWorkflow, "done!");
                Assert.Equal("done!", await handle.GetResultAsync());
                // Confirm log is present
                Assert.Contains(loggerFactory.Logs, entry => entry.Formatted == "Some log Bar");
                // Now clear and issue query and confirm log is not present
                loggerFactory.ClearLogs();
                Assert.InRange(
                    await handle.QueryAsync(MiscHelpersWorkflow.Ref.Random, 3), 0, 2);
                Assert.DoesNotContain(
                    loggerFactory.Logs, entry => entry.Formatted == "Some log Bar");
            },
            new()
            {
                LoggerFactory = loggerFactory,
            });

        // Run the worker again with a query so we can force replay
        await ExecuteWorkerAsync<MiscHelpersWorkflow>(
            async worker =>
            {
                // Now query after close to check that is replaying worked
                var isReplayingValues = await Env.Client.GetWorkflowHandle(workflowID).
                    QueryAsync(MiscHelpersWorkflow.Ref.GetEventsForIsReplaying);
                Assert.Equal(new[] { false, true }, isReplayingValues);
            },
            new(taskQueue: taskQueue));
    }

    [Workflow]
    public class WaitConditionWorkflow
    {
        public static readonly WaitConditionWorkflow Ref = Refs.Create<WaitConditionWorkflow>();

        private readonly CancellationTokenSource cancelSource;
        private bool shouldProceed;

        public WaitConditionWorkflow() =>
            cancelSource = CancellationTokenSource.CreateLinkedTokenSource(Workflow.CancellationToken);

        [WorkflowRun]
        public Task<bool> RunAsync(int timeoutMs) =>
            Workflow.WaitConditionAsync(() => shouldProceed, timeoutMs, cancelSource.Token);

        [WorkflowSignal]
        public async Task ProceedAsync() => shouldProceed = true;

        [WorkflowSignal]
        public async Task CancelWaitAsync() => cancelSource.Cancel();
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitCondition_Succeeds()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                WaitConditionWorkflow.Ref.RunAsync,
                Timeout.Infinite,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Tell to proceed and confirm it does
            await handle.SignalAsync(WaitConditionWorkflow.Ref.ProceedAsync);
            Assert.True(await handle.GetResultAsync());
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionManualCancel_ProperlyCancels()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                WaitConditionWorkflow.Ref.RunAsync,
                Timeout.Infinite,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Cancel and confirm it does
            await handle.SignalAsync(WaitConditionWorkflow.Ref.CancelWaitAsync);
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            var exc2 = Assert.IsType<ApplicationFailureException>(exc.InnerException);
            Assert.Contains("canceled", exc2.Message);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionWorkflowCancel_ProperlyCancels()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                WaitConditionWorkflow.Ref.RunAsync,
                Timeout.Infinite,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Confirm query says it's running
            await AssertStartedEventuallyAsync(handle);
            // Cancel workflow and confirm it does
            await handle.CancelAsync();
            var exc = await Assert.ThrowsAsync<WorkflowFailedException>(
                () => handle.GetResultAsync());
            Assert.IsType<CancelledFailureException>(exc.InnerException);
        });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_WaitConditionTimeout_ProperlyTimesOut()
    {
        await ExecuteWorkerAsync<WaitConditionWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                WaitConditionWorkflow.Ref.RunAsync,
                10,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
            // Wait for timeout
            Assert.False(await handle.GetResultAsync());
        });
    }

    [Workflow]
    public class DeadlockWorkflow
    {
        public static readonly DeadlockWorkflow Ref = Refs.Create<DeadlockWorkflow>();

        [WorkflowRun]
        public async Task RunAsync() => Thread.Sleep(4000);
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Deadlock_ProperlyFails()
    {
        await ExecuteWorkerAsync<DeadlockWorkflow>(
            async worker =>
            {
                // Start the workflow
                var handle = await Env.Client.StartWorkflowAsync(
                    DeadlockWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertTaskFailureContainsEventuallyAsync(handle, "deadlocked");
            },
            // Disable task tracing so we can add delay in there
            new() { DisableWorkflowTaskTracing = true });
    }

    [Workflow]
    public class SearchAttributesWorkflow
    {
        public static readonly SearchAttributesWorkflow Ref = Refs.Create<SearchAttributesWorkflow>();

        public static readonly SearchAttributeCollection AttributesInitial = new SearchAttributeCollection.Builder().
            Set(AttrBool, true).
            Set(AttrDateTime, new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)).
            Set(AttrDouble, 123.45).
            Set(AttrKeyword, "SomeKeyword").
            // TODO(cretz): Fix after Temporal dev server upgraded
            // Set(AttrKeywordList, new[] { "SomeKeyword1", "SomeKeyword2" }).
            Set(AttrLong, 678).
            Set(AttrText, "SomeText").
            ToSearchAttributeCollection();

        // Update half, remove half
        public static readonly SearchAttributeUpdate[] AttributeFirstUpdates = new SearchAttributeUpdate[]
        {
            AttrBool.ValueSet(false),
            AttrDateTime.ValueSet(new DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            AttrDouble.ValueSet(234.56),
            AttrKeyword.ValueUnset(),
            AttrLong.ValueUnset(),
            AttrText.ValueUnset(),
        };

        public static readonly SearchAttributeCollection AttributesFirstUpdated = new SearchAttributeCollection.Builder().
            Set(AttrBool, false).
            Set(AttrDateTime, new DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero)).
            Set(AttrDouble, 234.56).
            ToSearchAttributeCollection();

        // Update/remove other half
        public static readonly SearchAttributeUpdate[] AttributeSecondUpdates = new SearchAttributeUpdate[]
        {
            AttrBool.ValueUnset(),
            AttrDateTime.ValueUnset(),
            AttrDouble.ValueUnset(),
            AttrKeyword.ValueSet("AnotherKeyword"),
            AttrLong.ValueSet(789),
            AttrText.ValueSet("SomeOtherText"),
        };

        public static readonly SearchAttributeCollection AttributesSecondUpdated = new SearchAttributeCollection.Builder().
            Set(AttrKeyword, "AnotherKeyword").
            Set(AttrLong, 789).
            Set(AttrText, "SomeOtherText").
            ToSearchAttributeCollection();

        public static void AssertAttributesEqual(
            SearchAttributeCollection expected, SearchAttributeCollection actual) =>
            Assert.Equal(
                expected.UntypedValues.Where(kvp => kvp.Key.Name.StartsWith("DotNet")),
                actual.UntypedValues.Where(kvp => kvp.Key.Name.StartsWith("DotNet")));

        private bool proceed;

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Check initial
            AssertAttributesEqual(AttributesInitial, Workflow.TypedSearchAttributes);
            // Wait, then update and check
            await Workflow.WaitConditionAsync(() => proceed);
            proceed = false;
            Workflow.UpsertTypedSearchAttributes(AttributeFirstUpdates);
            AssertAttributesEqual(AttributesFirstUpdated, Workflow.TypedSearchAttributes);
            // Wait, then update and check
            await Workflow.WaitConditionAsync(() => proceed);
            proceed = false;
            Workflow.UpsertTypedSearchAttributes(AttributeSecondUpdates);
            AssertAttributesEqual(AttributesSecondUpdated, Workflow.TypedSearchAttributes);
        }

        [WorkflowSignal]
        public async Task ProceedAsync() => proceed = true;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_SearchAttributes_ProperlyUpserted()
    {
        await EnsureSearchAttributesPresentAsync();
        await ExecuteWorkerAsync<SearchAttributesWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                SearchAttributesWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    TypedSearchAttributes = SearchAttributesWorkflow.AttributesInitial,
                });
            // Confirm description shows initial
            SearchAttributesWorkflow.AssertAttributesEqual(
                SearchAttributesWorkflow.AttributesInitial,
                (await handle.DescribeAsync()).TypedSearchAttributes);
            // Tell workflow to proceed and confirm next values
            await handle.SignalAsync(SearchAttributesWorkflow.Ref.ProceedAsync);
            await AssertMore.EventuallyAsync(async () =>
                SearchAttributesWorkflow.AssertAttributesEqual(
                    SearchAttributesWorkflow.AttributesFirstUpdated,
                    (await handle.DescribeAsync()).TypedSearchAttributes));
            // Tell workflow to proceed and confirm next values
            await handle.SignalAsync(SearchAttributesWorkflow.Ref.ProceedAsync);
            await AssertMore.EventuallyAsync(async () =>
                SearchAttributesWorkflow.AssertAttributesEqual(
                    SearchAttributesWorkflow.AttributesSecondUpdated,
                    (await handle.DescribeAsync()).TypedSearchAttributes));
        });
    }

    [Workflow]
    public class MemoWorkflow
    {
        public static readonly MemoWorkflow Ref = Refs.Create<MemoWorkflow>();

        public static readonly Dictionary<string, object> MemoInitial = new()
        {
            ["foo"] = "fooval",
            ["bar"] = "barval",
        };

        public static readonly MemoUpdate[] MemoUpdates = new[]
        {
            MemoUpdate.ValueSet("foo", "newfooval"),
            MemoUpdate.ValueUnset("bar"),
            MemoUpdate.ValueSet("baz", "bazval"),
        };

        public static readonly Dictionary<string, object> MemoUpdated = new()
        {
            ["foo"] = "newfooval",
            ["baz"] = "bazval",
        };

        private bool proceed;

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Check initial
            Assert.Equal(MemoInitial, Workflow.Memo.ToDictionary(
                kvp => kvp.Key, kvp => (object)kvp.Value.ToValue<string>()));
            // Wait, then update and check
            await Workflow.WaitConditionAsync(() => proceed);
            Workflow.UpsertMemo(MemoUpdates);
            Assert.Equal(MemoUpdated, Workflow.Memo.ToDictionary(
                kvp => kvp.Key, kvp => (object)kvp.Value.ToValue<string>()));
        }

        [WorkflowSignal]
        public async Task ProceedAsync() => proceed = true;
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_Memo_ProperlyUpserted()
    {
        await ExecuteWorkerAsync<MemoWorkflow>(async worker =>
        {
            // Start the workflow
            var handle = await Env.Client.StartWorkflowAsync(
                MemoWorkflow.Ref.RunAsync,
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    Memo = MemoWorkflow.MemoInitial,
                });
            // Confirm description shows initial
            async Task<Dictionary<string, object>> GetCurrentMemoAsync()
            {
                var desc = await handle.DescribeAsync();
                var dict = new Dictionary<string, object>(desc.Memo.Count);
                foreach (var kvp in desc.Memo)
                {
                    dict[kvp.Key] = await kvp.Value.ToValueAsync<string>();
                }
                return dict;
            }
            Assert.Equal(MemoWorkflow.MemoInitial, await GetCurrentMemoAsync());
            // Tell workflow to proceed and confirm next values
            await handle.SignalAsync(MemoWorkflow.Ref.ProceedAsync);
            await AssertMore.EventuallyAsync(async () =>
                Assert.Equal(MemoWorkflow.MemoUpdated, await GetCurrentMemoAsync()));
        });
    }

    [Workflow]
    public class ContinueAsNewWorkflow
    {
        public static readonly ContinueAsNewWorkflow Ref = Refs.Create<ContinueAsNewWorkflow>();

        [WorkflowRun]
        public async Task<IList<string>> RunAsync(IList<string> pastRunIDs)
        {
            // Check memo and retry policy
            Assert.Equal(pastRunIDs.Count, Workflow.Memo["PastRunIDCount"].ToValue<int>());
            Assert.Equal(pastRunIDs.Count + 1000, Workflow.Info.RetryPolicy?.MaximumAttempts);

            if (pastRunIDs.Count == 5)
            {
                return pastRunIDs;
            }
            if (Workflow.Info.ContinuedRunID is string contRunID)
            {
                pastRunIDs.Add(contRunID);
            }
            throw Workflow.CreateContinueAsNewException(Ref.RunAsync, pastRunIDs, new()
            {
                Memo = new KeyValuePair<string, object>[]
                {
                    new("PastRunIDCount", pastRunIDs.Count),
                },
                RetryPolicy = new() { MaximumAttempts = pastRunIDs.Count + 1000 },
            });
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_ContinueAsNew_ProperlyContinues()
    {
        await ExecuteWorkerAsync<ContinueAsNewWorkflow>(async worker =>
        {
            var handle = await Env.Client.StartWorkflowAsync(
                ContinueAsNewWorkflow.Ref.RunAsync,
                new List<string>(),
                new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                {
                    Memo = new KeyValuePair<string, object>[] { new("PastRunIDCount", 0) },
                    RetryPolicy = new() { MaximumAttempts = 1000 },
                });
            var result = await handle.GetResultAsync();
            Assert.Equal(5, result.Count);
            Assert.Equal(handle.FirstExecutionRunID, result[0]);
        });
    }

    [Workflow]
    public class SimpleActivityWorkflow
    {
        public static readonly SimpleActivityWorkflow Ref = Refs.Create<SimpleActivityWorkflow>();

        [Activity]
        public static string ResultNoArgSync() => "1";

        [Activity]
        public static string ResultWithArgSync(string arg)
        {
            Assert.Equal("2", arg);
            return "3";
        }

        [Activity]
        public static void NoResultNoArgSync()
        {
        }

        [Activity]
        public static void NoResultWithArgSync(string arg) => Assert.Equal("4", arg);

        [Activity]
        public static async Task<string> ResultNoArgAsync() => "5";

        [Activity]
        public static async Task<string> ResultWithArgAsync(string arg)
        {
            Assert.Equal("6", arg);
            return "7";
        }

        [Activity]
        public static Task NoResultNoArgAsync() => Task.CompletedTask;

        [Activity]
        public static async Task NoResultWithArgAsync(string arg) => Assert.Equal("8", arg);

        [Activity]
        public static string ResultMultiArgSync(string arg1, string arg2)
        {
            Assert.Equal("9", arg1);
            Assert.Equal("10", arg2);
            return "11";
        }

        [Activity]
        public static void NoResultMultiArgSync(string arg1, string arg2)
        {
            Assert.Equal("12", arg1);
            Assert.Equal("13", arg2);
        }

        [WorkflowRun]
        public async Task RunAsync(bool local)
        {
#pragma warning disable SA1118 // Don't want so many lines
            // Intentionally not making options var because I want to confirm new() { ... } works
            if (local)
            {
                Assert.Equal("1", await Workflow.ExecuteLocalActivityAsync(
                    ResultNoArgSync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("3", await Workflow.ExecuteLocalActivityAsync(
                    ResultWithArgSync, "2", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    NoResultNoArgSync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteLocalActivityAsync(
                    NoResultWithArgSync, "4", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("5", await Workflow.ExecuteLocalActivityAsync(
                    ResultNoArgAsync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("7", await Workflow.ExecuteLocalActivityAsync(
                    ResultWithArgAsync, "6", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    NoResultNoArgAsync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteLocalActivityAsync(
                    NoResultWithArgAsync, "8", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("11", await Workflow.ExecuteLocalActivityAsync<string>(
                    ActivityDefinition.FromDelegate(ResultMultiArgSync).Name,
                    new object?[] { "9", "10" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteLocalActivityAsync(
                    ActivityDefinition.FromDelegate(NoResultMultiArgSync).Name,
                    new object?[] { "12", "13" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
            }
            else
            {
                Assert.Equal("1", await Workflow.ExecuteActivityAsync(
                    ResultNoArgSync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("3", await Workflow.ExecuteActivityAsync(
                    ResultWithArgSync, "2", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    NoResultNoArgSync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteActivityAsync(
                    NoResultWithArgSync, "4", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("5", await Workflow.ExecuteActivityAsync(
                    ResultNoArgAsync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                Assert.Equal("7", await Workflow.ExecuteActivityAsync(
                    ResultWithArgAsync, "6", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    NoResultNoArgAsync, new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                await Workflow.ExecuteActivityAsync(
                    NoResultWithArgAsync, "8", new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
                Assert.Equal("11", await Workflow.ExecuteActivityAsync<string>(
                    ActivityDefinition.FromDelegate(ResultMultiArgSync).Name,
                    new object?[] { "9", "10" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) }));
                await Workflow.ExecuteActivityAsync(
                    ActivityDefinition.FromDelegate(NoResultMultiArgSync).Name,
                    new object?[] { "12", "13" },
                    new() { ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
            }
#pragma warning restore SA1118
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteWorkflowAsync_SimpleActivity_ExecutesProperly(bool local)
    {
        await ExecuteWorkerAsync<SimpleActivityWorkflow>(
            async worker =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    SimpleActivityWorkflow.Ref.RunAsync,
                    local,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                var activities = new List<string>();
                await foreach (var evt in handle.FetchHistoryEventsAsync())
                {
                    if (local)
                    {
                        var attr = evt.MarkerRecordedEventAttributes;
                        if (attr != null)
                        {
                            Assert.Equal("core_local_activity", attr.MarkerName);
                            var data = await DataConverter.Default.ToValueAsync<System.Text.Json.JsonElement>(
                                attr.Details["data"].Payloads_.Single());
                            activities.Add(data.GetProperty("activity_type").GetString()!);
                        }
                    }
                    else
                    {
                        var attr = evt.ActivityTaskScheduledEventAttributes;
                        if (attr != null)
                        {
                            activities.Add(attr.ActivityType.Name);
                        }
                    }
                }
                Assert.Equal(
                    new List<string>()
                    {
                        "ResultNoArgSync",
                        "ResultWithArgSync",
                        "NoResultNoArgSync",
                        "NoResultWithArgSync",
                        "ResultNoArg",
                        "ResultWithArg",
                        "NoResultNoArg",
                        "NoResultWithArg",
                        "ResultMultiArgSync",
                        "NoResultMultiArgSync",
                    },
                    activities);
            },
            new()
            {
                Activities =
                {
                    SimpleActivityWorkflow.ResultNoArgSync,
                    SimpleActivityWorkflow.ResultWithArgSync,
                    SimpleActivityWorkflow.NoResultNoArgSync,
                    SimpleActivityWorkflow.NoResultWithArgSync,
                    SimpleActivityWorkflow.ResultNoArgAsync,
                    SimpleActivityWorkflow.ResultWithArgAsync,
                    SimpleActivityWorkflow.NoResultNoArgAsync,
                    SimpleActivityWorkflow.NoResultWithArgAsync,
                    SimpleActivityWorkflow.ResultMultiArgSync,
                    SimpleActivityWorkflow.NoResultMultiArgSync,
                },
            });
    }

    [Workflow]
    public class TimeoutActivityWorkflow
    {
        public static readonly TimeoutActivityWorkflow Ref = Refs.Create<TimeoutActivityWorkflow>();

        [Activity]
        public static Task RunUntilCancelledAsync() =>
            Task.Delay(Timeout.Infinite, ActivityContext.Current.CancellationToken);

        [WorkflowRun]
        public async Task RunAsync(bool local)
        {
            // Timeout after 10ms w/ no retry
            if (local)
            {
                await Workflow.ExecuteLocalActivityAsync(
                    RunUntilCancelledAsync,
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMilliseconds(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                    });
            }
            else
            {
                await Workflow.ExecuteActivityAsync(
                    RunUntilCancelledAsync,
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMilliseconds(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                    });
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteWorkflowAsync_TimeoutActivity_TimesOut(bool local)
    {
        await ExecuteWorkerAsync<TimeoutActivityWorkflow>(
            async worker =>
            {
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        TimeoutActivityWorkflow.Ref.RunAsync,
                        local,
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                // TODO(cretz): Local activities are not wrapping exceptions properly, update this
                // when https://github.com/temporalio/sdk-core/issues/323 fixed
                TimeoutFailureException toExc;
                if (local)
                {
                    toExc = Assert.IsType<TimeoutFailureException>(wfExc.InnerException);
                }
                else
                {
                    var actExc = Assert.IsType<ActivityFailureException>(wfExc.InnerException);
                    toExc = Assert.IsType<TimeoutFailureException>(actExc.InnerException);
                }
                Assert.Equal(TimeoutType.StartToClose, toExc.TimeoutType);
            },
            new() { Activities = { TimeoutActivityWorkflow.RunUntilCancelledAsync } });
    }

    [Workflow]
    public class CancelActivityWorkflow
    {
        public static readonly CancelActivityWorkflow Ref = Refs.Create<CancelActivityWorkflow>();

        [Activity]
        public static Task RunUntilCancelledAsync() =>
            Task.Delay(Timeout.Infinite, ActivityContext.Current.CancellationToken);

        [WorkflowRun]
        public async Task RunAsync(Input input)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                Workflow.CancellationToken);
            // Cancel before if desired
            if (input.BeforeStart)
            {
                tokenSource.Cancel();
            }
            Task activity;
            if (input.Local)
            {
                activity = Workflow.ExecuteLocalActivityAsync(
                    RunUntilCancelledAsync,
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                        CancellationToken = tokenSource.Token,
                    });
            }
            else
            {
                activity = Workflow.ExecuteActivityAsync(
                    RunUntilCancelledAsync,
                    new()
                    {
                        StartToCloseTimeout = TimeSpan.FromMinutes(10),
                        RetryPolicy = new() { MaximumAttempts = 1 },
                        CancellationToken = tokenSource.Token,
                    });
            }
            // If not cancel before, wait one ms (i.e. roll the task over), then cancel
            if (!input.BeforeStart)
            {
                await Workflow.DelayAsync(1);
                tokenSource.Cancel();
            }
            await activity;
        }

        public record Input(bool Local, bool BeforeStart);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteWorkflowAsync_CancelActivity_Cancels(bool local)
    {
        await ExecuteWorkerAsync<CancelActivityWorkflow>(
            async worker =>
            {
                // Regular cancel after start
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        CancelActivityWorkflow.Ref.RunAsync,
                        new CancelActivityWorkflow.Input(local, BeforeStart: false),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                        {
                            // Lowering for quicker LA result
                            TaskTimeout = TimeSpan.FromSeconds(2),
                        }));
                // TODO(cretz): Local activities are not wrapping exceptions properly, update this
                // when https://github.com/temporalio/sdk-core/issues/323 fixed
                if (local)
                {
                    Assert.IsType<CancelledFailureException>(wfExc.InnerException);
                }
                else
                {
                    var actExc = Assert.IsType<ActivityFailureException>(wfExc.InnerException);
                    Assert.IsType<CancelledFailureException>(actExc.InnerException);
                }

                // Cancel before start
                wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        CancelActivityWorkflow.Ref.RunAsync,
                        new CancelActivityWorkflow.Input(local, BeforeStart: true),
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                        {
                            // Lowering for quicker LA result
                            TaskTimeout = TimeSpan.FromSeconds(2),
                        }));
                var cancelExc = Assert.IsType<CancelledFailureException>(wfExc.InnerException);
                Assert.Contains("cancelled before scheduled", cancelExc.Message);
            },
            new() { Activities = { CancelActivityWorkflow.RunUntilCancelledAsync } });
    }

    [Workflow]
    public class SimpleChildWorkflow
    {
        [Workflow]
        public class ResultNoArg
        {
            public static readonly ResultNoArg Ref = Refs.Create<ResultNoArg>();

            [WorkflowRun]
            public async Task<string> RunAsync() => "1";
        }

        [Workflow]
        public class ResultWithArg
        {
            public static readonly ResultWithArg Ref = Refs.Create<ResultWithArg>();

            [WorkflowRun]
            public async Task<string> RunAsync(string arg)
            {
                Assert.Equal("2", arg);
                return "3";
            }
        }

        [Workflow]
        public class NoResultNoArg
        {
            public static readonly NoResultNoArg Ref = Refs.Create<NoResultNoArg>();

            [WorkflowRun]
            public Task RunAsync() => Task.CompletedTask;
        }

        [Workflow]
        public class NoResultWithArg
        {
            public static readonly NoResultWithArg Ref = Refs.Create<NoResultWithArg>();

            [WorkflowRun]
            public async Task RunAsync(string arg) => Assert.Equal("4", arg);
        }

        [Workflow]
        public class ResultMultiArg
        {
            public static readonly ResultMultiArg Ref = Refs.Create<ResultMultiArg>();

            [WorkflowRun]
            public async Task<string> RunAsync(string arg1, string arg2)
            {
                Assert.Equal("5", arg1);
                Assert.Equal("6", arg2);
                return "7";
            }
        }

        [Workflow]
        public class NoResultMultiArg
        {
            public static readonly NoResultMultiArg Ref = Refs.Create<NoResultMultiArg>();

            [WorkflowRun]
            public async Task RunAsync(string arg1, string arg2)
            {
                Assert.Equal("8", arg1);
                Assert.Equal("9", arg2);
            }
        }

        public static readonly SimpleChildWorkflow Ref = Refs.Create<SimpleChildWorkflow>();

        [WorkflowRun]
        public async Task RunAsync()
        {
#pragma warning disable SA1118 // Don't want so many lines
            // Intentionally not making options var because I want to confirm new() { ... } works
            Assert.Equal("1", await Workflow.ExecuteChildWorkflowAsync(
                ResultNoArg.Ref.RunAsync, new() { RunTimeout = TimeSpan.FromHours(1) }));
            Assert.Equal("3", await Workflow.ExecuteChildWorkflowAsync(
                ResultWithArg.Ref.RunAsync, "2", new() { RunTimeout = TimeSpan.FromHours(1) }));
            await Workflow.ExecuteChildWorkflowAsync(
                NoResultNoArg.Ref.RunAsync, new() { RunTimeout = TimeSpan.FromHours(1) });
            await Workflow.ExecuteChildWorkflowAsync(
                NoResultWithArg.Ref.RunAsync, "4", new() { RunTimeout = TimeSpan.FromHours(1) });
            Assert.Equal("7", await Workflow.ExecuteChildWorkflowAsync<string>(
                WorkflowDefinition.FromType(typeof(ResultMultiArg)).Name,
                new object?[] { "5", "6" },
                new() { RunTimeout = TimeSpan.FromHours(1) }));
            await Workflow.ExecuteChildWorkflowAsync(
                WorkflowDefinition.FromType(typeof(NoResultMultiArg)).Name,
                new object?[] { "8", "9" },
                new() { RunTimeout = TimeSpan.FromHours(1) });
#pragma warning restore SA1118
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_SimpleChild_ExecutesProperly()
    {
        await ExecuteWorkerAsync<SimpleChildWorkflow>(
            async worker =>
            {
                var handle = await Env.Client.StartWorkflowAsync(
                    SimpleChildWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await handle.GetResultAsync();
                var children = new List<string>();
                await foreach (var evt in handle.FetchHistoryEventsAsync())
                {
                    var attr = evt.ChildWorkflowExecutionStartedEventAttributes;
                    if (attr != null)
                    {
                        children.Add(attr.WorkflowType.Name);
                    }
                }
                Assert.Equal(
                    new List<string>()
                    {
                        "ResultNoArg",
                        "ResultWithArg",
                        "NoResultNoArg",
                        "NoResultWithArg",
                        "ResultMultiArg",
                        "NoResultMultiArg",
                    },
                    children);
            },
            new()
            {
                Workflows =
                {
                    typeof(SimpleChildWorkflow.ResultNoArg),
                    typeof(SimpleChildWorkflow.ResultWithArg),
                    typeof(SimpleChildWorkflow.NoResultNoArg),
                    typeof(SimpleChildWorkflow.NoResultWithArg),
                    typeof(SimpleChildWorkflow.ResultMultiArg),
                    typeof(SimpleChildWorkflow.NoResultMultiArg),
                },
            });
    }

    [Workflow]
    public class TimeoutChildWorkflow
    {
        [Workflow]
        public class ChildWorkflow
        {
            public static readonly ChildWorkflow Ref = Refs.Create<ChildWorkflow>();

            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        public static readonly TimeoutChildWorkflow Ref = Refs.Create<TimeoutChildWorkflow>();

        [WorkflowRun]
        public Task RunAsync() =>
            // Timeout after 10ms
            Workflow.ExecuteChildWorkflowAsync(
                ChildWorkflow.Ref.RunAsync, new() { RunTimeout = TimeSpan.FromMilliseconds(10) });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_TimeoutChild_TimesOut()
    {
        await ExecuteWorkerAsync<TimeoutChildWorkflow>(
            async worker =>
            {
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        TimeoutChildWorkflow.Ref.RunAsync,
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var actExc = Assert.IsType<ChildWorkflowFailureException>(wfExc.InnerException);
                var toExc = Assert.IsType<TimeoutFailureException>(actExc.InnerException);
                Assert.Equal(TimeoutType.StartToClose, toExc.TimeoutType);
            },
            new() { Workflows = { typeof(TimeoutChildWorkflow.ChildWorkflow) } });
    }

    [Workflow]
    public class CancelChildWorkflow
    {
        [Workflow]
        public class ChildWorkflow
        {
            public static readonly ChildWorkflow Ref = Refs.Create<ChildWorkflow>();

            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        public static readonly CancelChildWorkflow Ref = Refs.Create<CancelChildWorkflow>();

        [WorkflowRun]
        public async Task RunAsync(bool beforeStart)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                Workflow.CancellationToken);
            // Cancel before if desired
            if (beforeStart)
            {
                tokenSource.Cancel();
            }
            var handle = await Workflow.StartChildWorkflowAsync(
                ChildWorkflow.Ref.RunAsync,
                new() { CancellationToken = tokenSource.Token });
            // If not cancel before, cancel now
            if (!beforeStart)
            {
                tokenSource.Cancel();
            }
            await handle.GetResultAsync();
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_CancelChild_Cancels()
    {
        await ExecuteWorkerAsync<CancelChildWorkflow>(
            async worker =>
            {
                // Regular cancel after start
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        CancelChildWorkflow.Ref.RunAsync,
                        false,
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var childExc = Assert.IsType<ChildWorkflowFailureException>(wfExc.InnerException);
                Assert.IsType<CancelledFailureException>(childExc.InnerException);

                // Cancel before start
                wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        CancelChildWorkflow.Ref.RunAsync,
                        true,
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                var cancelExc = Assert.IsType<CancelledFailureException>(wfExc.InnerException);
                Assert.Contains("cancelled before scheduled", cancelExc.Message);
            },
            new() { Workflows = { typeof(CancelChildWorkflow.ChildWorkflow) } });
    }

    [Workflow]
    public class AlreadyStartedChildWorkflow
    {
        [Workflow]
        public class ChildWorkflow
        {
            public static readonly ChildWorkflow Ref = Refs.Create<ChildWorkflow>();

            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        public static readonly AlreadyStartedChildWorkflow Ref = Refs.Create<AlreadyStartedChildWorkflow>();

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Try to start a child workflow twice with the same ID
            var handle = await Workflow.StartChildWorkflowAsync(ChildWorkflow.Ref.RunAsync);
            await Workflow.StartChildWorkflowAsync(
                ChildWorkflow.Ref.RunAsync, new() { ID = handle.ID });
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_AlreadyStartedChild_FailsProperly()
    {
        await ExecuteWorkerAsync<AlreadyStartedChildWorkflow>(
            async worker =>
            {
                // Regular cancel after start
                var wfExc = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                    Env.Client.ExecuteWorkflowAsync(
                        AlreadyStartedChildWorkflow.Ref.RunAsync,
                        new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)));
                Assert.IsType<FailureException>(wfExc.InnerException);
                Assert.Contains("already started", wfExc.InnerException.Message);
            },
            new() { Workflows = { typeof(AlreadyStartedChildWorkflow.ChildWorkflow) } });
    }

    [Workflow]
    public class StackTraceWorkflow
    {
        [Activity]
        public static async Task WaitCancelActivityAsync()
        {
            while (!ActivityContext.Current.CancellationToken.IsCancellationRequested)
            {
                ActivityContext.Current.Heartbeat();
                await Task.Delay(100);
            }
        }

        [Workflow]
        public class WaitForeverWorkflow
        {
            public static readonly WaitForeverWorkflow Ref = Refs.Create<WaitForeverWorkflow>();

            [WorkflowRun]
            public Task RunAsync() => Workflow.DelayAsync(Timeout.Infinite);
        }

        public static readonly StackTraceWorkflow Ref = Refs.Create<StackTraceWorkflow>();

        private string status = "created";

        [WorkflowRun]
        public async Task RunAsync()
        {
            // Start multiple tasks and wait on them all
            await Task.WhenAll(
                Workflow.DelayAsync(TimeSpan.FromHours(2)),
                Workflow.ExecuteActivityAsync(
                    WaitCancelActivityAsync,
                    new()
                    {
                        ScheduleToCloseTimeout = TimeSpan.FromHours(2),
                        HeartbeatTimeout = TimeSpan.FromSeconds(2),
                    }),
                Workflow.ExecuteChildWorkflowAsync(WaitForeverWorkflow.Ref.RunAsync),
                WaitForever());
        }

        [WorkflowQuery]
        public string Status() => status;

        private async Task WaitForever()
        {
            status = "waiting";
            await Workflow.WaitConditionAsync(() => false);
        }
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StackTrace_FailsWhenDisabled()
    {
        await ExecuteWorkerAsync<StackTraceWorkflow>(
            async worker =>
            {
                // Start and wait until "waiting"
                var handle = await Env.Client.StartWorkflowAsync(
                    StackTraceWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.EqualEventuallyAsync(
                    "waiting", () => handle.QueryAsync(StackTraceWorkflow.Ref.Status));
                var exc = await Assert.ThrowsAsync<WorkflowQueryFailedException>(
                    () => handle.QueryAsync<string>("__stack_trace", Array.Empty<object?>()));
                Assert.Contains("stack traces are not enabled", exc.Message);
            },
            new()
            {
                Activities = { StackTraceWorkflow.WaitCancelActivityAsync },
                Workflows = { typeof(StackTraceWorkflow.WaitForeverWorkflow) },
            });
    }

    [Fact]
    public async Task ExecuteWorkflowAsync_StackTrace_ReportedProperlyWhenEnabled()
    {
        await ExecuteWorkerAsync<StackTraceWorkflow>(
            async worker =>
            {
                // Start and wait until "waiting"
                var handle = await Env.Client.StartWorkflowAsync(
                    StackTraceWorkflow.Ref.RunAsync,
                    new(id: $"workflow-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!));
                await AssertMore.EqualEventuallyAsync(
                    "waiting", () => handle.QueryAsync(StackTraceWorkflow.Ref.Status));
                // Issue stack trace query
                var trace = await handle.QueryAsync<string>("__stack_trace", Array.Empty<object?>());
                // Confirm our four tasks are the only ones there, are in order, and are waiting at
                // the expected spot
                var traces = trace.Split("\n\n");
                Assert.Equal(4, traces.Length);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.DelayAsync",
                    traces[0]);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.ExecuteActivityAsync",
                    traces[1]);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.StartChildWorkflowAsync",
                    traces[2]);
                Assert.StartsWith(
                    "Task waiting at:\n   at Temporalio.Workflows.Workflow.WaitConditionAsync",
                    traces[3]);
            },
            new()
            {
                Activities = { StackTraceWorkflow.WaitCancelActivityAsync },
                Workflows = { typeof(StackTraceWorkflow.WaitForeverWorkflow) },
                WorkflowStackTrace = WorkflowStackTrace.Normal,
            });
    }

    private async Task ExecuteWorkerAsync<TWf>(
        Func<TemporalWorker, Task> action, TemporalWorkerOptions? options = null)
    {
        options ??= new();
        options.TaskQueue ??= $"tq-{Guid.NewGuid()}";
        options.AddWorkflow(typeof(TWf));
        // options.Interceptors ??= new[] { new XunitExceptionInterceptor() };
        using var worker = new TemporalWorker(Client, options);
        await worker.ExecuteAsync(() => action(worker));
    }

    private static Task AssertTaskFailureContainsEventuallyAsync(
        WorkflowHandle handle, string messageContains)
    {
        return AssertTaskFailureEventuallyAsync(
            handle, attrs => Assert.Contains(messageContains, attrs.Failure?.Message));
    }

    private static Task AssertTaskFailureEventuallyAsync(
        WorkflowHandle handle, Action<WorkflowTaskFailedEventAttributes> assert)
    {
        return AssertMore.EventuallyAsync(async () =>
        {
            WorkflowTaskFailedEventAttributes? attrs = null;
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (evt.WorkflowTaskFailedEventAttributes != null)
                {
                    attrs = evt.WorkflowTaskFailedEventAttributes;
                }
            }
            Assert.NotNull(attrs);
            assert(attrs!);
        });
    }

    private static Task AssertStartedEventuallyAsync(WorkflowHandle handle)
    {
        return AssertHasEventEventuallyAsync(
            handle, e => e.WorkflowExecutionStartedEventAttributes != null);
    }

    private static Task AssertChildInitiatedEventuallyAsync(WorkflowHandle handle)
    {
        return AssertHasEventEventuallyAsync(
            handle, e => e.StartChildWorkflowExecutionInitiatedEventAttributes != null);
    }

    private static Task AssertHasEventEventuallyAsync(
        WorkflowHandle handle, Func<HistoryEvent, bool> predicate)
    {
        return AssertMore.EventuallyAsync(async () =>
        {
            await foreach (var evt in handle.FetchHistoryEventsAsync())
            {
                if (predicate(evt))
                {
                    return;
                }
            }
            Assert.Fail("Event not found");
        });
    }

    // TODO(cretz): Tests needed:
    // * Stack trace query
    //   * Consider leveraging https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stacktracehiddenattribute in newer versions
    // * Patching
    // * Separate interface from impl
    // * Workflows as records with WorkflowInit
    // * Workflows as structs
    // * TaskFactory default cancellation token (test with dataflow ReceiveAsync without explicit
    //   cancellation token)
    // * IDisposable workflows?
    //   * Otherwise, what if I have a cancellation token source at a high level?
    // * Custom errors
    // * Child workflows
    //   * Signal
    // * External workflows
    //   * Cancel
    //   * Signal
    // * Async utilities (e.g. Dataflow) that can/can't be used
    // * Custom codec
    // * Interceptor
    // * Dynamic activities
    // * Dynamic workflows
    // * Dynamic signals/queries
    // * Tracing
    //   * CorrelationManager?
    //   * EventSource?
    // * "WorkflowRunner" abstraction for trying out sandbox techniques
}